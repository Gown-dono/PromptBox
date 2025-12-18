using Microsoft.Win32;
using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PromptBox.Controls;

/// <summary>
/// Canvas for visual workflow editing with drag-drop, zoom, pan, undo/redo, and minimap
/// </summary>
public partial class WorkflowCanvas : UserControl
{
    private readonly Dictionary<string, WorkflowNode> _nodeMap = new();
    private readonly List<WorkflowConnector> _connectors = new();
    
    // Undo/Redo stacks
    private readonly Stack<WorkflowState> _undoStack = new();
    private readonly Stack<WorkflowState> _redoStack = new();
    private const int MaxUndoSteps = 50;
    
    // Clipboard for copy/paste
    private WorkflowStep? _clipboardStep;
    
    private bool _isConnecting;
    private string? _connectionSourceStepId;
    private string? _connectionBranchLabel;
    private Line? _connectionPreviewLine;
    private Point _panStartPoint;
    private bool _isPanning;
    private WorkflowNode? _selectedNode;
    
    // Bug 10 Fix: Flag to suppress events during state restoration
    private bool _isRestoring = false;
    
    // Bug 3 Fix: Throttled connector updates for smooth dragging
    private readonly DispatcherTimer _connectorUpdateTimer;
    private bool _connectorsNeedUpdate = false;
    
    private const int GridSize = 16;
    private const double MinimapScale = 0.08;
    
    #region Dependency Properties
    
    public static readonly DependencyProperty WorkflowProperty =
        DependencyProperty.Register(nameof(Workflow), typeof(Workflow), typeof(WorkflowCanvas),
            new PropertyMetadata(null, OnWorkflowChanged));
    
    public Workflow? Workflow
    {
        get => (Workflow?)GetValue(WorkflowProperty);
        set => SetValue(WorkflowProperty, value);
    }
    
    #endregion
    
    #region Events
    
    public event EventHandler<WorkflowStep>? StepSelected;
    public event EventHandler<WorkflowStep>? StepDoubleClicked;
    public event EventHandler? WorkflowModified;
    
    #endregion
    
    public WorkflowNode? SelectedNode => _selectedNode;
    
    public WorkflowCanvas()
    {
        InitializeComponent();
        
        // Handle keyboard shortcuts
        KeyDown += WorkflowCanvas_KeyDown;
        Focusable = true;
        
        // Bug 3 Fix: Initialize connector update timer for throttled updates (60 FPS)
        _connectorUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _connectorUpdateTimer.Tick += (s, e) =>
        {
            if (_connectorsNeedUpdate)
            {
                UpdateAllConnectors();
                _connectorsNeedUpdate = false;
            }
        };
        _connectorUpdateTimer.Start();
        
        // Update minimap when scroll changes (after loaded to ensure elements exist)
        Loaded += (s, e) =>
        {
            if (CanvasScrollViewer != null)
            {
                // Bug 11 Fix: Update minimap viewport on scroll
                CanvasScrollViewer.ScrollChanged += CanvasScrollViewer_ScrollChanged;
            }
        };
    }
    
    /// <summary>
    /// Bug 11 Fix: Handle scroll changes to update minimap viewport
    /// </summary>
    private void CanvasScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateMinimapViewport();
    }
    
    #region Property Changed
    
    private static void OnWorkflowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WorkflowCanvas canvas)
        {
            canvas.LoadWorkflow();
        }
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Loads the workflow onto the canvas
    /// </summary>
    public void LoadWorkflow()
    {
        try
        {
            // Safety check - ensure canvas elements are initialized
            if (NodesCanvas == null || ConnectorsCanvas == null) return;
            
            ClearCanvas();
            
            if (Workflow == null) return;
            
            // Ensure all steps have positions
            EnsureStepPositions();
            
            // Create nodes
            for (int i = 0; i < Workflow.Steps.Count; i++)
            {
                var step = Workflow.Steps[i];
                AddNodeInternal(step, i + 1);
            }
            
            // Create connectors (initial positions will be updated after layout)
            CreateConnectors();
            
            // Update minimap
            UpdateMinimap();
            
            // Force layout update and then update connectors
            // Use Render priority which runs after layout is complete
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
            {
                // Force measure/arrange on all nodes to ensure ActualWidth/Height are set
                foreach (var node in _nodeMap.Values)
                {
                    node.UpdateLayout();
                }
                
                UpdateAllConnectors();
                UpdateMinimap();
            }));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading workflow: {ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
        }
    }
    
    /// <summary>
    /// Adds a new step to the workflow
    /// </summary>
    public WorkflowStep AddStep(WorkflowStepType type, Point? position = null)
    {
        var step = new WorkflowStep
        {
            StepId = Guid.NewGuid().ToString(),
            Name = type switch
            {
                WorkflowStepType.Conditional => "Conditional Branch",
                WorkflowStepType.Loop => "Loop",
                _ => $"Step {(Workflow?.Steps.Count ?? 0) + 1}"
            },
            StepType = type,
            Order = Workflow?.Steps.Count ?? 0,
            Position = position ?? GetNextAvailablePosition(),
            IsStartStep = Workflow?.Steps.Count == 0
        };
        
        if (type == WorkflowStepType.Loop)
        {
            step.LoopConfig = new LoopConfig();
        }
        
        Workflow?.Steps.Add(step);
        AddNodeInternal(step, Workflow?.Steps.Count ?? 1);
        
        WorkflowModified?.Invoke(this, EventArgs.Empty);
        
        return step;
    }
    
    /// <summary>
    /// Removes a step from the workflow
    /// </summary>
    public void RemoveStep(string stepId)
    {
        if (Workflow == null) return;
        
        var step = Workflow.Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step == null) return;
        
        // Remove node
        if (_nodeMap.TryGetValue(stepId, out var node))
        {
            NodesCanvas.Children.Remove(node);
            _nodeMap.Remove(stepId);
        }
        
        // Remove connectors
        var connectorsToRemove = _connectors
            .Where(c => c.FromStepId == stepId || c.ToStepId == stepId)
            .ToList();
        
        foreach (var connector in connectorsToRemove)
        {
            connector.Cleanup(); // Clean up flow indicators
            ConnectorsCanvas.Children.Remove(connector);
            _connectors.Remove(connector);
        }
        
        // Update references in other steps
        foreach (var s in Workflow.Steps)
        {
            if (s.NextStepId == stepId)
                s.NextStepId = null;
            
            s.ConditionalBranches.RemoveAll(b => b.NextStepId == stepId);
            
            if (s.ErrorHandling?.FallbackStepId == stepId)
                s.ErrorHandling.FallbackStepId = null;
        }
        
        // Remove step
        Workflow.Steps.Remove(step);
        
        // Renumber steps
        for (int i = 0; i < Workflow.Steps.Count; i++)
        {
            Workflow.Steps[i].Order = i;
            if (_nodeMap.TryGetValue(Workflow.Steps[i].StepId, out var n))
            {
                n.StepNumber = i + 1;
            }
        }
        
        WorkflowModified?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Adds a connection between two steps
    /// </summary>
    public void AddConnection(string fromStepId, string toStepId, string? label = null)
    {
        if (Workflow == null) return;
        
        var fromStep = Workflow.Steps.FirstOrDefault(s => s.StepId == fromStepId);
        var toStep = Workflow.Steps.FirstOrDefault(s => s.StepId == toStepId);
        
        if (fromStep == null || toStep == null) return;
        
        // Check for circular dependency
        if (WouldCreateCycle(fromStepId, toStepId))
        {
            MessageBox.Show("Cannot create connection: would create a circular dependency.",
                "Invalid Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // Update step connection
        if (fromStep.StepType == WorkflowStepType.Conditional && !string.IsNullOrEmpty(label))
        {
            // Check if branch already exists
            var existingBranch = fromStep.ConditionalBranches.FirstOrDefault(b => b.Label == label);
            if (existingBranch != null)
            {
                // Remove old connector if exists
                var oldConnector = _connectors.FirstOrDefault(c => c.FromStepId == fromStepId && c.Label == label);
                if (oldConnector != null)
                {
                    oldConnector.Cleanup(); // Clean up flow indicators
                    ConnectorsCanvas?.Children.Remove(oldConnector);
                    _connectors.Remove(oldConnector);
                }
                
                // Update existing branch
                existingBranch.NextStepId = toStepId;
            }
            else
            {
                // Create new branch
                fromStep.ConditionalBranches.Add(new ConditionalBranch
                {
                    Label = label,
                    NextStepId = toStepId,
                    Condition = new ConditionEvaluator()
                });
            }
            
            // Create visual connector
            CreateConnector(fromStep, toStep, label, ConnectorType.Conditional);
        }
        else
        {
            // Remove old connector if exists for standard connection
            var oldConnector = _connectors.FirstOrDefault(c => c.FromStepId == fromStepId && string.IsNullOrEmpty(c.Label));
            if (oldConnector != null)
            {
                oldConnector.Cleanup(); // Clean up flow indicators
                ConnectorsCanvas?.Children.Remove(oldConnector);
                _connectors.Remove(oldConnector);
            }
            
            fromStep.NextStepId = toStepId;
            
            // Create visual connector
            CreateConnector(fromStep, toStep, label);
        }
        
        WorkflowModified?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Removes a connection between two steps
    /// </summary>
    public void RemoveConnection(string fromStepId, string toStepId)
    {
        var connector = _connectors.FirstOrDefault(c => c.FromStepId == fromStepId && c.ToStepId == toStepId);
        if (connector != null)
        {
            connector.Cleanup(); // Clean up flow indicators
            ConnectorsCanvas.Children.Remove(connector);
            _connectors.Remove(connector);
        }
        
        if (Workflow == null) return;
        
        var fromStep = Workflow.Steps.FirstOrDefault(s => s.StepId == fromStepId);
        if (fromStep != null)
        {
            if (fromStep.NextStepId == toStepId)
                fromStep.NextStepId = null;
            
            fromStep.ConditionalBranches.RemoveAll(b => b.NextStepId == toStepId);
        }
        
        WorkflowModified?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Auto-arranges nodes in a hierarchical layout
    /// </summary>
    public void AutoLayout()
    {
        if (Workflow == null || Workflow.Steps.Count == 0) return;
        
        // Bug 7 Fix: Save undo state before auto layout
        SaveUndoState("Auto Layout");
        
        const int horizontalSpacing = 250;
        const int verticalSpacing = 150;
        const int startX = 50;
        const int startY = 50;
        
        // Find start step
        var startStep = Workflow.GetStartStep();
        if (startStep == null) return;
        
        var visited = new HashSet<string>();
        var levels = new Dictionary<string, int>();
        var positions = new Dictionary<string, int>();
        
        // BFS to assign levels
        var queue = new Queue<(string stepId, int level)>();
        queue.Enqueue((startStep.StepId, 0));
        var levelCounts = new Dictionary<int, int>();
        
        while (queue.Count > 0)
        {
            var (stepId, level) = queue.Dequeue();
            
            if (visited.Contains(stepId)) continue;
            visited.Add(stepId);
            
            levels[stepId] = level;
            
            if (!levelCounts.ContainsKey(level))
                levelCounts[level] = 0;
            positions[stepId] = levelCounts[level]++;
            
            var step = Workflow.GetStepById(stepId);
            if (step == null) continue;
            
            // Add next steps
            if (!string.IsNullOrEmpty(step.NextStepId))
                queue.Enqueue((step.NextStepId, level + 1));
            
            foreach (var branch in step.ConditionalBranches)
            {
                if (!string.IsNullOrEmpty(branch.NextStepId))
                    queue.Enqueue((branch.NextStepId, level + 1));
            }
        }
        
        // Apply positions to reachable nodes
        foreach (var step in Workflow.Steps)
        {
            if (levels.TryGetValue(step.StepId, out var level) && positions.TryGetValue(step.StepId, out var pos))
            {
                step.Position = new Point(startX + pos * horizontalSpacing, startY + level * verticalSpacing);
                step.HasValidPosition = true; // Bug 5 Fix: Mark position as valid
                
                if (_nodeMap.TryGetValue(step.StepId, out var node))
                {
                    Canvas.SetLeft(node, step.Position.X);
                    Canvas.SetTop(node, step.Position.Y);
                }
            }
        }
        
        // Bug 6 Fix: Handle unreachable/orphaned nodes
        var unreachable = Workflow.Steps.Where(s => !visited.Contains(s.StepId)).ToList();
        if (unreachable.Count > 0)
        {
            var maxLevel = levels.Values.Any() ? levels.Values.Max() : 0;
            var orphanY = startY + (maxLevel + 2) * verticalSpacing;
            
            for (int i = 0; i < unreachable.Count; i++)
            {
                var step = unreachable[i];
                step.Position = new Point(startX + i * horizontalSpacing, orphanY);
                step.HasValidPosition = true;
                
                if (_nodeMap.TryGetValue(step.StepId, out var node))
                {
                    Canvas.SetLeft(node, step.Position.X);
                    Canvas.SetTop(node, step.Position.Y);
                }
            }
        }
        
        // Update connectors
        UpdateAllConnectors();
        UpdateMinimap();
        
        WorkflowModified?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Updates the execution status of a node
    /// </summary>
    public void SetNodeExecutionStatus(string stepId, NodeExecutionStatus status)
    {
        if (_nodeMap.TryGetValue(stepId, out var node))
        {
            node.ExecutionStatus = status;
            node.IsExecuting = status == NodeExecutionStatus.Running;
        }
    }
    
    /// <summary>
    /// Resets all nodes to pending status
    /// </summary>
    public void ResetExecutionStatus()
    {
        foreach (var node in _nodeMap.Values)
        {
            node.ExecutionStatus = NodeExecutionStatus.Pending;
            node.IsExecuting = false;
        }
    }
    
    /// <summary>
    /// Duplicates the selected node
    /// </summary>
    public WorkflowStep? DuplicateSelectedNode()
    {
        if (_selectedNode?.Step == null || Workflow == null) return null;
        
        SaveUndoState("Duplicate Node");
        
        var original = _selectedNode.Step;
        var duplicate = new WorkflowStep
        {
            StepId = Guid.NewGuid().ToString(),
            Name = $"{original.Name} (Copy)",
            Description = original.Description,
            PromptTemplate = original.PromptTemplate,
            StepType = original.StepType,
            Order = Workflow.Steps.Count,
            Position = new Point(original.Position.X + 50, original.Position.Y + 50),
            UsesPreviousOutput = original.UsesPreviousOutput,
            OutputVariable = original.OutputVariable,
            IsStartStep = false,
            IsEndStep = false,
            ErrorHandling = original.ErrorHandling != null ? new ErrorHandlingConfig
            {
                MaxRetries = original.ErrorHandling.MaxRetries,
                RetryDelayMs = original.ErrorHandling.RetryDelayMs,
                UseExponentialBackoff = original.ErrorHandling.UseExponentialBackoff,
                ContinueOnError = original.ErrorHandling.ContinueOnError
            } : null,
            LoopConfig = original.LoopConfig != null ? new LoopConfig
            {
                MaxIterations = original.LoopConfig.MaxIterations,
                LoopVariable = original.LoopConfig.LoopVariable,
                ExitCondition = new ConditionEvaluator
                {
                    Type = original.LoopConfig.ExitCondition.Type,
                    ComparisonValue = original.LoopConfig.ExitCondition.ComparisonValue,
                    Operator = original.LoopConfig.ExitCondition.Operator
                }
            } : null
        };
        
        Workflow.Steps.Add(duplicate);
        AddNodeInternal(duplicate, Workflow.Steps.Count);
        
        WorkflowModified?.Invoke(this, EventArgs.Empty);
        UpdateMinimap();
        
        return duplicate;
    }
    
    /// <summary>
    /// Copies the selected node to clipboard
    /// </summary>
    public void CopySelectedNode()
    {
        if (_selectedNode?.Step == null) return;
        _clipboardStep = _selectedNode.Step;
    }
    
    /// <summary>
    /// Pastes the clipboard node
    /// </summary>
    public WorkflowStep? PasteNode()
    {
        if (_clipboardStep == null || Workflow == null) return null;
        
        SaveUndoState("Paste Node");
        
        var paste = new WorkflowStep
        {
            StepId = Guid.NewGuid().ToString(),
            Name = $"{_clipboardStep.Name} (Pasted)",
            Description = _clipboardStep.Description,
            PromptTemplate = _clipboardStep.PromptTemplate,
            StepType = _clipboardStep.StepType,
            Order = Workflow.Steps.Count,
            Position = new Point(_clipboardStep.Position.X + 100, _clipboardStep.Position.Y + 100),
            UsesPreviousOutput = _clipboardStep.UsesPreviousOutput,
            OutputVariable = _clipboardStep.OutputVariable,
            IsStartStep = false,
            IsEndStep = false
        };
        
        Workflow.Steps.Add(paste);
        AddNodeInternal(paste, Workflow.Steps.Count);
        
        WorkflowModified?.Invoke(this, EventArgs.Empty);
        UpdateMinimap();
        
        return paste;
    }
    
    /// <summary>
    /// Undoes the last action
    /// </summary>
    public void Undo()
    {
        if (_undoStack.Count == 0 || Workflow == null) return;
        
        // Save current state to redo stack
        _redoStack.Push(CaptureCurrentState("Redo"));
        
        // Restore previous state
        var state = _undoStack.Pop();
        RestoreState(state);
        
        UpdateUndoRedoButtons();
        
        // Bug 10 Fix: Only invoke WorkflowModified if not restoring
        if (!_isRestoring)
            WorkflowModified?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Redoes the last undone action
    /// </summary>
    public void Redo()
    {
        if (_redoStack.Count == 0 || Workflow == null) return;
        
        // Save current state to undo stack
        _undoStack.Push(CaptureCurrentState("Undo"));
        
        // Restore redo state
        var state = _redoStack.Pop();
        RestoreState(state);
        
        UpdateUndoRedoButtons();
        
        // Bug 10 Fix: Only invoke WorkflowModified if not restoring
        if (!_isRestoring)
            WorkflowModified?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Exports the canvas as a PNG image
    /// </summary>
    public void ExportAsPng(string filePath)
    {
        var bounds = GetWorkflowBounds();
        var margin = 50;
        
        var width = (int)(bounds.Width + margin * 2);
        var height = (int)(bounds.Height + margin * 2);
        
        var renderBitmap = new RenderTargetBitmap(
            width, height, 96, 96, PixelFormats.Pbgra32);
        
        // Create a visual to render
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // Draw background
            context.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                null,
                new Rect(0, 0, width, height));
            
            // Draw grid
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), 1);
            for (int x = 0; x < width; x += GridSize)
            {
                context.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                    null,
                    new Point(x, 0), 1, 1);
            }
            
            // Offset for margin
            context.PushTransform(new TranslateTransform(margin - bounds.X, margin - bounds.Y));
            
            // Draw connectors
            foreach (var connector in _connectors)
            {
                var pen = new Pen(connector.ConnectorType switch
                {
                    ConnectorType.Conditional => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    ConnectorType.Loop => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    ConnectorType.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    _ => new SolidColorBrush(Color.FromRgb(100, 100, 100))
                }, 2);
                
                var geometry = new PathGeometry();
                var figure = new PathFigure { StartPoint = connector.StartPoint };
                
                var controlPoint1 = new Point(connector.StartPoint.X, connector.StartPoint.Y + 50);
                var controlPoint2 = new Point(connector.EndPoint.X, connector.EndPoint.Y - 50);
                figure.Segments.Add(new BezierSegment(controlPoint1, controlPoint2, connector.EndPoint, true));
                geometry.Figures.Add(figure);
                
                context.DrawGeometry(null, pen, geometry);
                
                // Draw arrow
                var arrowSize = 8;
                var arrowGeometry = new PathGeometry();
                var arrowFigure = new PathFigure { StartPoint = connector.EndPoint };
                arrowFigure.Segments.Add(new LineSegment(new Point(connector.EndPoint.X - arrowSize, connector.EndPoint.Y - arrowSize), true));
                arrowFigure.Segments.Add(new LineSegment(new Point(connector.EndPoint.X + arrowSize, connector.EndPoint.Y - arrowSize), true));
                arrowFigure.IsClosed = true;
                arrowGeometry.Figures.Add(arrowFigure);
                context.DrawGeometry(pen.Brush, null, arrowGeometry);
            }
            
            // Draw nodes
            foreach (var step in Workflow!.Steps)
            {
                DrawNodeForExport(context, step);
            }
            
            context.Pop();
        }
        
        renderBitmap.Render(visual);
        
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
        
        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }
    
    /// <summary>
    /// Exports the workflow as JSON
    /// </summary>
    public void ExportAsJson(string filePath)
    {
        if (Workflow == null) return;
        
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(Workflow, options);
        File.WriteAllText(filePath, json);
    }
    
    #endregion
    
    #region Undo/Redo Helpers
    
    private void SaveUndoState(string actionName)
    {
        if (Workflow == null) return;
        
        _undoStack.Push(CaptureCurrentState(actionName));
        _redoStack.Clear();
        
        // Limit undo stack size
        while (_undoStack.Count > MaxUndoSteps)
        {
            var temp = new Stack<WorkflowState>();
            for (int i = 0; i < MaxUndoSteps; i++)
            {
                temp.Push(_undoStack.Pop());
            }
            _undoStack.Clear();
            while (temp.Count > 0)
            {
                _undoStack.Push(temp.Pop());
            }
        }
        
        UpdateUndoRedoButtons();
    }
    
    private WorkflowState CaptureCurrentState(string actionName)
    {
        return new WorkflowState
        {
            ActionName = actionName,
            StepsJson = JsonSerializer.Serialize(Workflow!.Steps)
        };
    }
    
    private void RestoreState(WorkflowState state)
    {
        if (Workflow == null) return;
        
        // Bug 10 Fix: Set restoring flag to suppress events during restore
        _isRestoring = true;
        
        try
        {
            var steps = JsonSerializer.Deserialize<List<WorkflowStep>>(state.StepsJson);
            if (steps == null) return;
            
            // Bug 9 Fix: Deep clone steps to avoid reference sharing
            Workflow.Steps.Clear();
            foreach (var step in steps)
            {
                Workflow.Steps.Add(CloneStep(step));
            }
            
            // Bug 10 Fix: Reload workflow to refresh visual canvas
            LoadWorkflow();
        }
        finally
        {
            _isRestoring = false;
        }
    }
    
    /// <summary>
    /// Bug 9 Fix: Deep clones a workflow step to avoid reference sharing in undo/redo
    /// </summary>
    private static WorkflowStep CloneStep(WorkflowStep original)
    {
        return new WorkflowStep
        {
            StepId = original.StepId,
            Order = original.Order,
            Name = original.Name,
            Description = original.Description,
            PromptTemplate = original.PromptTemplate,
            UsesPreviousOutput = original.UsesPreviousOutput,
            OutputVariable = original.OutputVariable,
            StepType = original.StepType,
            NextStepId = original.NextStepId,
            Position = original.Position,
            HasValidPosition = original.HasValidPosition,
            IsStartStep = original.IsStartStep,
            IsEndStep = original.IsEndStep,
            ConditionalBranches = original.ConditionalBranches.Select(b => new ConditionalBranch
            {
                Label = b.Label,
                NextStepId = b.NextStepId,
                Condition = new ConditionEvaluator
                {
                    Type = b.Condition.Type,
                    ComparisonValue = b.Condition.ComparisonValue,
                    Operator = b.Condition.Operator
                }
            }).ToList(),
            ErrorHandling = original.ErrorHandling != null ? new ErrorHandlingConfig
            {
                MaxRetries = original.ErrorHandling.MaxRetries,
                RetryDelayMs = original.ErrorHandling.RetryDelayMs,
                UseExponentialBackoff = original.ErrorHandling.UseExponentialBackoff,
                FallbackStepId = original.ErrorHandling.FallbackStepId,
                ContinueOnError = original.ErrorHandling.ContinueOnError
            } : null,
            LoopConfig = original.LoopConfig != null ? new LoopConfig
            {
                MaxIterations = original.LoopConfig.MaxIterations,
                LoopVariable = original.LoopConfig.LoopVariable,
                ExitCondition = new ConditionEvaluator
                {
                    Type = original.LoopConfig.ExitCondition.Type,
                    ComparisonValue = original.LoopConfig.ExitCondition.ComparisonValue,
                    Operator = original.LoopConfig.ExitCondition.Operator
                }
            } : null,
            ParallelConfig = original.ParallelConfig != null ? new ParallelConfig
            {
                BranchStepIds = new List<string>(original.ParallelConfig.BranchStepIds),
                WaitForAll = original.ParallelConfig.WaitForAll,
                ContinueOnBranchFailure = original.ParallelConfig.ContinueOnBranchFailure,
                OutputVariablePrefix = original.ParallelConfig.OutputVariablePrefix
            } : null
        };
    }
    
    private void UpdateUndoRedoButtons()
    {
        // Safety checks for initialization
        if (UndoButton == null || RedoButton == null || UndoRedoText == null) return;
        
        UndoButton.IsEnabled = _undoStack.Count > 0;
        RedoButton.IsEnabled = _redoStack.Count > 0;
        
        var undoText = _undoStack.Count > 0 ? $"Undo: {_undoStack.Peek().ActionName}" : "";
        UndoRedoText.Text = undoText;
    }
    
    #endregion
    
    #region Minimap Helpers
    
    private void UpdateMinimap()
    {
        // Safety checks for initialization
        if (MinimapCanvas == null || Minimap == null || MinimapViewport == null) return;
        if (Workflow == null || Workflow.Steps.Count == 0)
        {
            MinimapCanvas.Children.Clear();
            return;
        }
        
        MinimapCanvas.Children.Clear();
        
        var bounds = GetWorkflowBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        
        var scale = Math.Min(
            (Minimap.Width - 20) / bounds.Width,
            (Minimap.Height - 30) / bounds.Height);
        scale = Math.Min(scale, MinimapScale * 2);
        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale)) return;
        
        // Draw nodes on minimap
        foreach (var step in Workflow.Steps)
        {
            var rect = new Rectangle
            {
                Width = 200 * scale,
                Height = 80 * scale,
                Fill = step.StepType switch
                {
                    WorkflowStepType.Conditional => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    WorkflowStepType.Loop => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    _ => new SolidColorBrush(Color.FromRgb(103, 58, 183))
                },
                RadiusX = 4,
                RadiusY = 4
            };
            
            Canvas.SetLeft(rect, (step.Position.X - bounds.X) * scale + 5);
            Canvas.SetTop(rect, (step.Position.Y - bounds.Y) * scale + 20);
            MinimapCanvas.Children.Add(rect);
        }
        
        // Draw connectors on minimap
        foreach (var connector in _connectors)
        {
            var line = new Line
            {
                X1 = (connector.StartPoint.X - bounds.X) * scale + 5 + 100 * scale,
                Y1 = (connector.StartPoint.Y - bounds.Y) * scale + 20,
                X2 = (connector.EndPoint.X - bounds.X) * scale + 5 + 100 * scale,
                Y2 = (connector.EndPoint.Y - bounds.Y) * scale + 20,
                Stroke = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                StrokeThickness = 1
            };
            MinimapCanvas.Children.Add(line);
        }
        
        // Bug 11 Fix: Update viewport indicator separately
        UpdateMinimapViewport();
    }
    
    /// <summary>
    /// Bug 11 Fix: Updates only the minimap viewport indicator (for scroll events)
    /// </summary>
    private void UpdateMinimapViewport()
    {
        if (MinimapViewport == null || Minimap == null || CanvasScrollViewer == null || CanvasScale == null) return;
        if (Workflow == null || Workflow.Steps.Count == 0) return;
        
        var bounds = GetWorkflowBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        
        var scale = Math.Min(
            (Minimap.Width - 20) / bounds.Width,
            (Minimap.Height - 30) / bounds.Height);
        scale = Math.Min(scale, MinimapScale * 2);
        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale)) return;
        
        // Update viewport indicator
        var viewportWidth = CanvasScrollViewer.ViewportWidth / CanvasScale.ScaleX * scale;
        var viewportHeight = CanvasScrollViewer.ViewportHeight / CanvasScale.ScaleY * scale;
        var viewportX = (CanvasScrollViewer.HorizontalOffset / CanvasScale.ScaleX - bounds.X) * scale + 5;
        var viewportY = (CanvasScrollViewer.VerticalOffset / CanvasScale.ScaleY - bounds.Y) * scale + 20;
        
        MinimapViewport.Width = Math.Max(20, viewportWidth);
        MinimapViewport.Height = Math.Max(15, viewportHeight);
        MinimapViewport.Margin = new Thickness(Math.Max(0, viewportX), Math.Max(20, viewportY), 0, 0);
    }
    
    private Rect GetWorkflowBounds()
    {
        if (Workflow == null || Workflow.Steps.Count == 0)
            return new Rect(0, 0, 500, 400);
        
        var minX = Workflow.Steps.Min(s => s.Position.X);
        var minY = Workflow.Steps.Min(s => s.Position.Y);
        var maxX = Workflow.Steps.Max(s => s.Position.X) + 200;
        var maxY = Workflow.Steps.Max(s => s.Position.Y) + 100;
        
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
    
    private void DrawNodeForExport(DrawingContext context, WorkflowStep step)
    {
        var nodeWidth = 200;
        var nodeHeight = 100;
        var cornerRadius = 8;
        
        // Node background
        var bgBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        var bgRect = new Rect(step.Position.X, step.Position.Y, nodeWidth, nodeHeight);
        context.DrawRoundedRectangle(bgBrush, new Pen(new SolidColorBrush(Color.FromRgb(200, 200, 200)), 1),
            bgRect, cornerRadius, cornerRadius);
        
        // Header
        var headerBrush = step.StepType switch
        {
            WorkflowStepType.Conditional => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            WorkflowStepType.Loop => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            _ => new SolidColorBrush(Color.FromRgb(103, 58, 183))
        };
        
        var headerGeometry = new PathGeometry();
        var headerFigure = new PathFigure { StartPoint = new Point(step.Position.X + cornerRadius, step.Position.Y) };
        headerFigure.Segments.Add(new LineSegment(new Point(step.Position.X + nodeWidth - cornerRadius, step.Position.Y), true));
        headerFigure.Segments.Add(new ArcSegment(new Point(step.Position.X + nodeWidth, step.Position.Y + cornerRadius), new Size(cornerRadius, cornerRadius), 0, false, SweepDirection.Clockwise, true));
        headerFigure.Segments.Add(new LineSegment(new Point(step.Position.X + nodeWidth, step.Position.Y + 30), true));
        headerFigure.Segments.Add(new LineSegment(new Point(step.Position.X, step.Position.Y + 30), true));
        headerFigure.Segments.Add(new LineSegment(new Point(step.Position.X, step.Position.Y + cornerRadius), true));
        headerFigure.Segments.Add(new ArcSegment(new Point(step.Position.X + cornerRadius, step.Position.Y), new Size(cornerRadius, cornerRadius), 0, false, SweepDirection.Clockwise, true));
        headerFigure.IsClosed = true;
        headerGeometry.Figures.Add(headerFigure);
        context.DrawGeometry(headerBrush, null, headerGeometry);
        
        // Step number
        var numberText = new FormattedText(
            (step.Order + 1).ToString(),
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            11,
            Brushes.White,
            96);
        context.DrawText(numberText, new Point(step.Position.X + 10, step.Position.Y + 8));
        
        // Step name
        var nameText = new FormattedText(
            step.Name,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            12,
            Brushes.White,
            96);
        context.DrawText(nameText, new Point(step.Position.X + 35, step.Position.Y + 7));
        
        // Prompt preview
        var preview = step.PromptTemplate.Length > 50 ? step.PromptTemplate[..50] + "..." : step.PromptTemplate;
        var previewText = new FormattedText(
            preview,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            10,
            new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            96);
        context.DrawText(previewText, new Point(step.Position.X + 10, step.Position.Y + 40));
    }
    
    #endregion
    
    #region Private Methods
    
    private void ClearCanvas()
    {
        // Clean up all connectors and their flow indicators
        foreach (var connector in _connectors)
        {
            connector.Cleanup();
        }
        
        NodesCanvas?.Children.Clear();
        ConnectorsCanvas?.Children.Clear();
        FlowIndicatorsCanvas?.Children.Clear(); // Also clear the flow indicators canvas
        _nodeMap.Clear();
        _connectors.Clear();
        _selectedNode = null;
    }
    
    private void EnsureStepPositions()
    {
        if (Workflow == null) return;
        
        const int startX = 50;
        const int startY = 50;
        const int verticalSpacing = 150;
        
        for (int i = 0; i < Workflow.Steps.Count; i++)
        {
            var step = Workflow.Steps[i];
            
            // Bug 5 Fix: Only reposition if HasValidPosition is false
            // This allows (0,0) to be a valid intentional position
            if (!step.HasValidPosition)
            {
                step.Position = new Point(startX, startY + i * verticalSpacing);
                step.HasValidPosition = true;
            }
            
            // Ensure StepId
            if (string.IsNullOrEmpty(step.StepId))
            {
                step.StepId = Guid.NewGuid().ToString();
            }
            
            // Set start step if first
            if (i == 0 && !Workflow.Steps.Any(s => s.IsStartStep))
            {
                step.IsStartStep = true;
            }
        }
    }
    
    private void AddNodeInternal(WorkflowStep step, int stepNumber)
    {
        if (NodesCanvas == null || step == null) return;
        
        var node = new WorkflowNode
        {
            Step = step,
            StepNumber = stepNumber
        };
        
        // Bug 2 Fix: Pass snap function to node for smooth snapping during drag
        node.SnapToGridFunc = SnapToGrid;
        
        // Enhancement 3: Pass alignment function to node for grid alignment guides
        node.FindAlignmentFunc = FindAlignmentGuides;
        node.AlignmentChanged += Node_AlignmentChanged;
        
        Canvas.SetLeft(node, step.Position.X);
        Canvas.SetTop(node, step.Position.Y);
        
        node.NodeSelected += Node_Selected;
        node.NodeMoved += Node_Moved;
        node.InputConnectorClicked += Node_InputConnectorClicked;
        node.OutputConnectorClicked += Node_OutputConnectorClicked;
        node.BranchConnectorClicked += Node_BranchConnectorClicked;
        node.AddBranchRequested += Node_AddBranchRequested;
        node.MouseDoubleClick += Node_DoubleClick;
        
        NodesCanvas.Children.Add(node);
        _nodeMap[step.StepId] = node;
    }
    
    private void CreateConnectors()
    {
        if (Workflow == null) return;
        
        foreach (var step in Workflow.Steps)
        {
            // Standard next step connection
            if (!string.IsNullOrEmpty(step.NextStepId))
            {
                var toStep = Workflow.GetStepById(step.NextStepId);
                if (toStep != null)
                {
                    CreateConnector(step, toStep);
                }
            }
            
            // Conditional branches
            foreach (var branch in step.ConditionalBranches)
            {
                if (!string.IsNullOrEmpty(branch.NextStepId))
                {
                    var toStep = Workflow.GetStepById(branch.NextStepId);
                    if (toStep != null)
                    {
                        CreateConnector(step, toStep, branch.Label, ConnectorType.Conditional);
                    }
                }
            }
        }
    }
    
    private void CreateConnector(WorkflowStep fromStep, WorkflowStep toStep, string? label = null, ConnectorType type = ConnectorType.Standard)
    {
        if (ConnectorsCanvas == null) return;
        
        if (!_nodeMap.TryGetValue(fromStep.StepId, out var fromNode) ||
            !_nodeMap.TryGetValue(toStep.StepId, out var toNode))
            return;
        
        Point startPoint;
        Point endPoint;
        
        // For conditional branches, start from the right side branch connector
        if (type == ConnectorType.Conditional && !string.IsNullOrEmpty(label))
        {
            var branchIndex = fromStep.ConditionalBranches.FindIndex(b => b.Label == label);
            if (branchIndex < 0) branchIndex = 0;
            
            startPoint = fromNode.GetBranchConnectorCenter(branchIndex);
            endPoint = toNode.GetLeftConnectorCenter();
        }
        else
        {
            // Standard connection: bottom to top
            startPoint = fromNode.GetOutputConnectorCenter();
            endPoint = toNode.GetInputConnectorCenter();
        }
        
        var connector = new WorkflowConnector
        {
            FromStepId = fromStep.StepId,
            ToStepId = toStep.StepId,
            Label = label ?? string.Empty,
            ConnectorType = type,
            StartPoint = startPoint,
            EndPoint = endPoint,
            FlowIndicatorsCanvas = FlowIndicatorsCanvas // Set the flow indicators canvas so dots appear above nodes
        };
        
        ConnectorsCanvas.Children.Add(connector);
        _connectors.Add(connector);
    }
    
    private void UpdateAllConnectors()
    {
        foreach (var connector in _connectors)
        {
            if (_nodeMap.TryGetValue(connector.FromStepId, out var fromNode) &&
                _nodeMap.TryGetValue(connector.ToStepId, out var toNode))
            {
                // For conditional branches, connect from right side branch to left side of target
                if (connector.ConnectorType == ConnectorType.Conditional && !string.IsNullOrEmpty(connector.Label) && fromNode.Step != null)
                {
                    var branchIndex = fromNode.Step.ConditionalBranches.FindIndex(b => b.Label == connector.Label);
                    if (branchIndex < 0) branchIndex = 0;
                    
                    connector.StartPoint = fromNode.GetBranchConnectorCenter(branchIndex);
                    connector.EndPoint = toNode.GetLeftConnectorCenter();
                }
                else
                {
                    // Standard connection: bottom to top
                    connector.StartPoint = fromNode.GetOutputConnectorCenter();
                    connector.EndPoint = toNode.GetInputConnectorCenter();
                }
            }
        }
    }
    
    private Point GetNextAvailablePosition()
    {
        const int startX = 50;
        const int startY = 50;
        const int spacing = 150;
        const int nodeWidth = 200;
        const int nodeHeight = 150;
        
        // Bug 8 Fix: Check both X and Y to find truly available space
        // Use grid-based collision detection to avoid overlaps
        for (int y = startY; y < 2000; y += spacing)
        {
            for (int x = startX; x < 2000; x += spacing)
            {
                var testPos = new Point(x, y);
                var hasCollision = Workflow?.Steps.Any(s =>
                    Math.Abs(s.Position.X - testPos.X) < nodeWidth &&
                    Math.Abs(s.Position.Y - testPos.Y) < nodeHeight) ?? false;
                
                if (!hasCollision)
                    return testPos;
            }
        }
        
        // Fallback to simple Y-based positioning if no space found
        var maxY = Workflow?.Steps.Count > 0
            ? Workflow.Steps.Max(s => s.Position.Y)
            : startY - spacing;
        
        return new Point(startX, maxY + spacing);
    }
    
    private Point SnapToGrid(Point point)
    {
        if (SnapToGridCheckBox == null || SnapToGridCheckBox.IsChecked != true) return point;
        
        return new Point(
            Math.Round(point.X / GridSize) * GridSize,
            Math.Round(point.Y / GridSize) * GridSize
        );
    }
    
    /// <summary>
    /// Enhancement 3: Finds alignment guides when dragging a node near other nodes' edges
    /// </summary>
    private AlignmentResult FindAlignmentGuides(Point position, string excludeStepId)
    {
        const double alignmentThreshold = 8; // Snap when within 8px
        const double nodeWidth = 200;
        const double nodeHeight = 112;
        
        var result = new AlignmentResult();
        
        if (Workflow == null) return result;
        
        // Get edges of the dragged node
        var draggedLeft = position.X;
        var draggedRight = position.X + nodeWidth;
        var draggedTop = position.Y;
        var draggedBottom = position.Y + nodeHeight;
        var draggedCenterX = position.X + nodeWidth / 2;
        var draggedCenterY = position.Y + nodeHeight / 2;
        
        foreach (var step in Workflow.Steps)
        {
            if (step.StepId == excludeStepId) continue;
            
            var otherLeft = step.Position.X;
            var otherRight = step.Position.X + nodeWidth;
            var otherTop = step.Position.Y;
            var otherBottom = step.Position.Y + nodeHeight;
            var otherCenterX = step.Position.X + nodeWidth / 2;
            var otherCenterY = step.Position.Y + nodeHeight / 2;
            
            // Check vertical alignments (X positions)
            // Left edge alignment
            if (Math.Abs(draggedLeft - otherLeft) < alignmentThreshold)
            {
                result.SnapToX = otherLeft;
                result.VerticalLines.Add(otherLeft);
            }
            // Right edge alignment
            else if (Math.Abs(draggedRight - otherRight) < alignmentThreshold)
            {
                result.SnapToX = otherRight - nodeWidth;
                result.VerticalLines.Add(otherRight);
            }
            // Center X alignment
            else if (Math.Abs(draggedCenterX - otherCenterX) < alignmentThreshold)
            {
                result.SnapToX = otherCenterX - nodeWidth / 2;
                result.VerticalLines.Add(otherCenterX);
            }
            // Left to right alignment
            else if (Math.Abs(draggedLeft - otherRight) < alignmentThreshold)
            {
                result.SnapToX = otherRight;
                result.VerticalLines.Add(otherRight);
            }
            // Right to left alignment
            else if (Math.Abs(draggedRight - otherLeft) < alignmentThreshold)
            {
                result.SnapToX = otherLeft - nodeWidth;
                result.VerticalLines.Add(otherLeft);
            }
            
            // Check horizontal alignments (Y positions)
            // Top edge alignment
            if (Math.Abs(draggedTop - otherTop) < alignmentThreshold)
            {
                result.SnapToY = otherTop;
                result.HorizontalLines.Add(otherTop);
            }
            // Bottom edge alignment
            else if (Math.Abs(draggedBottom - otherBottom) < alignmentThreshold)
            {
                result.SnapToY = otherBottom - nodeHeight;
                result.HorizontalLines.Add(otherBottom);
            }
            // Center Y alignment
            else if (Math.Abs(draggedCenterY - otherCenterY) < alignmentThreshold)
            {
                result.SnapToY = otherCenterY - nodeHeight / 2;
                result.HorizontalLines.Add(otherCenterY);
            }
            // Top to bottom alignment
            else if (Math.Abs(draggedTop - otherBottom) < alignmentThreshold)
            {
                result.SnapToY = otherBottom;
                result.HorizontalLines.Add(otherBottom);
            }
            // Bottom to top alignment
            else if (Math.Abs(draggedBottom - otherTop) < alignmentThreshold)
            {
                result.SnapToY = otherTop - nodeHeight;
                result.HorizontalLines.Add(otherTop);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Enhancement 3: Handles alignment guide changes to draw/clear guide lines
    /// </summary>
    private void Node_AlignmentChanged(object? sender, AlignmentResult alignment)
    {
        UpdateAlignmentGuides(alignment);
    }
    
    /// <summary>
    /// Enhancement 3: Draws or clears alignment guide lines on the canvas
    /// </summary>
    private void UpdateAlignmentGuides(AlignmentResult alignment)
    {
        if (AlignmentGuidesCanvas == null) return;
        
        AlignmentGuidesCanvas.Children.Clear();
        
        if (!alignment.HasAlignment) return;
        
        var guideBrush = new SolidColorBrush(Color.FromArgb(180, 255, 64, 129)); // Pink/magenta
        var guideStroke = new DoubleCollection { 4, 2 };
        
        // Draw vertical alignment lines
        foreach (var x in alignment.VerticalLines)
        {
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = 1500,
                Stroke = guideBrush,
                StrokeThickness = 1,
                StrokeDashArray = guideStroke
            };
            AlignmentGuidesCanvas.Children.Add(line);
        }
        
        // Draw horizontal alignment lines
        foreach (var y in alignment.HorizontalLines)
        {
            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = 2000,
                Y2 = y,
                Stroke = guideBrush,
                StrokeThickness = 1,
                StrokeDashArray = guideStroke
            };
            AlignmentGuidesCanvas.Children.Add(line);
        }
    }
    
    private bool WouldCreateCycle(string fromStepId, string toStepId)
    {
        if (Workflow == null) return false;
        
        // Check if toStep can reach fromStep (would create cycle)
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(toStepId);
        
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (currentId == fromStepId) return true;
            if (visited.Contains(currentId)) continue;
            visited.Add(currentId);
            
            var step = Workflow.GetStepById(currentId);
            if (step == null) continue;
            
            if (!string.IsNullOrEmpty(step.NextStepId))
                queue.Enqueue(step.NextStepId);
            
            foreach (var branch in step.ConditionalBranches)
            {
                if (!string.IsNullOrEmpty(branch.NextStepId))
                    queue.Enqueue(branch.NextStepId);
            }
        }
        
        return false;
    }
    
    private void SelectNode(WorkflowNode? node)
    {
        if (_selectedNode != null)
            _selectedNode.IsSelected = false;
        
        _selectedNode = node;
        
        if (_selectedNode != null)
        {
            _selectedNode.IsSelected = true;
            StepSelected?.Invoke(this, _selectedNode.Step!);
        }
    }
    
    private void StartConnectionMode(string sourceStepId, string? branchLabel = null)
    {
        _isConnecting = true;
        _connectionSourceStepId = sourceStepId;
        _connectionBranchLabel = branchLabel;
        if (ConnectionModeIndicator != null)
            ConnectionModeIndicator.Visibility = Visibility.Visible;
        Cursor = Cursors.Cross;
    }
    
    private void EndConnectionMode()
    {
        _isConnecting = false;
        _connectionSourceStepId = null;
        _connectionBranchLabel = null;
        if (ConnectionModeIndicator != null)
            ConnectionModeIndicator.Visibility = Visibility.Collapsed;
        Cursor = Cursors.Arrow;
        
        if (_connectionPreviewLine != null && PreviewCanvas != null)
        {
            PreviewCanvas.Children.Remove(_connectionPreviewLine);
            _connectionPreviewLine = null;
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    private void Node_Selected(object? sender, WorkflowNode node)
    {
        if (_isConnecting && _connectionSourceStepId != null && node.Step != null)
        {
            // Complete connection - use branch label if we're connecting from a branch
            AddConnection(_connectionSourceStepId, node.Step.StepId, _connectionBranchLabel);
            EndConnectionMode();
        }
        else
        {
            SelectNode(node);
        }
    }
    
    private void Node_Moved(object? sender, Point newPosition)
    {
        if (sender is WorkflowNode node && node.Step != null)
        {
            // Bug 2 Fix: Position is already snapped in WorkflowNode.NodeCard_MouseMove
            // No need to snap again here - just update canvas position
            Canvas.SetLeft(node, node.Step.Position.X);
            Canvas.SetTop(node, node.Step.Position.Y);
            
            // Bug 3 Fix: Set flag for throttled connector update instead of immediate update
            _connectorsNeedUpdate = true;
            
            // Bug 10 Fix: Only invoke WorkflowModified if not restoring
            if (!_isRestoring)
                WorkflowModified?.Invoke(this, EventArgs.Empty);
        }
    }
    
    private void Node_InputConnectorClicked(object? sender, string stepId)
    {
        if (_isConnecting && _connectionSourceStepId != null)
        {
            AddConnection(_connectionSourceStepId, stepId, _connectionBranchLabel);
            EndConnectionMode();
        }
    }
    
    private void Node_OutputConnectorClicked(object? sender, string stepId)
    {
        StartConnectionMode(stepId);
    }
    
    private void Node_BranchConnectorClicked(object? sender, string branchLabel)
    {
        if (sender is WorkflowNode node && node.Step != null)
        {
            // Start connection mode with branch label context
            StartConnectionMode(node.Step.StepId, branchLabel);
        }
    }
    
    private void Node_AddBranchRequested(object? sender, string branchLabel)
    {
        if (sender is WorkflowNode node && node.Step != null)
        {
            // Add a new branch to the conditional step
            node.Step.ConditionalBranches.Add(new ConditionalBranch
            {
                Label = branchLabel,
                NextStepId = string.Empty,
                Condition = new ConditionEvaluator()
            });
            
            // Refresh the node to show the new branch
            node.Step = node.Step; // Trigger property changed
            
            // Start connection mode for the new branch
            StartConnectionMode(node.Step.StepId, branchLabel);
            
            WorkflowModified?.Invoke(this, EventArgs.Empty);
        }
    }
    
    private void Node_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is WorkflowNode node && node.Step != null)
        {
            StepDoubleClicked?.Invoke(this, node.Step);
        }
    }
    
    private void NodesCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isConnecting)
        {
            EndConnectionMode();
        }
        else
        {
            SelectNode(null);
        }
        
        Focus();
    }
    
    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        AddStep(WorkflowStepType.Standard);
    }
    
    private void AddConditional_Click(object sender, RoutedEventArgs e)
    {
        AddStep(WorkflowStepType.Conditional);
    }
    
    private void AddLoop_Click(object sender, RoutedEventArgs e)
    {
        AddStep(WorkflowStepType.Loop);
    }
    
    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode?.Step != null)
        {
            StartConnectionMode(_selectedNode.Step.StepId);
        }
        else
        {
            MessageBox.Show("Please select a node first.", "Connect", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode?.Step != null)
        {
            var result = MessageBox.Show($"Delete step '{_selectedNode.Step.Name}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                RemoveStep(_selectedNode.Step.StepId);
                SelectNode(null);
            }
        }
    }
    
    private void AutoLayout_Click(object sender, RoutedEventArgs e)
    {
        AutoLayout();
    }
    
    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CanvasScale == null || ZoomText == null) return;
        
        var scale = e.NewValue / 100.0;
        CanvasScale.ScaleX = scale;
        CanvasScale.ScaleY = scale;
        ZoomText.Text = $"{(int)e.NewValue}%";
        
        // Note: Connector thickness compensation removed - now using animated flow indicators instead of arrows
        
        UpdateMinimap();
    }
    
    private void CanvasScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && ZoomSlider != null)
        {
            var delta = e.Delta > 0 ? 10 : -10;
            ZoomSlider.Value = Math.Clamp(ZoomSlider.Value + delta, 50, 200);
            e.Handled = true;
        }
    }
    
    private void CanvasScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (CanvasScrollViewer == null) return;
        
        if (e.MiddleButton == MouseButtonState.Pressed ||
            (e.LeftButton == MouseButtonState.Pressed && Keyboard.Modifiers == ModifierKeys.Control))
        {
            _isPanning = true;
            _panStartPoint = e.GetPosition(CanvasScrollViewer);
            CanvasScrollViewer.CaptureMouse();
            Cursor = Cursors.Hand;
        }
    }
    
    private void CanvasScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning && CanvasScrollViewer != null)
        {
            var currentPoint = e.GetPosition(CanvasScrollViewer);
            var delta = _panStartPoint - currentPoint;
            
            CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset + delta.X);
            CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset + delta.Y);
            
            _panStartPoint = currentPoint;
        }
        else if (_isConnecting && _connectionSourceStepId != null && NodesCanvas != null && PreviewCanvas != null)
        {
            // Update connection preview line
            var mousePos = e.GetPosition(NodesCanvas);
            
            if (_connectionPreviewLine == null)
            {
                _connectionPreviewLine = new Line
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                PreviewCanvas.Children.Add(_connectionPreviewLine);
            }
            
            if (_nodeMap.TryGetValue(_connectionSourceStepId, out var sourceNode) && sourceNode.Step != null)
            {
                Point startPoint;
                
                // If connecting from a branch, use branch connector position
                if (!string.IsNullOrEmpty(_connectionBranchLabel))
                {
                    var branchIndex = sourceNode.Step.ConditionalBranches.FindIndex(b => b.Label == _connectionBranchLabel);
                    if (branchIndex < 0) branchIndex = 0;
                    startPoint = sourceNode.GetBranchConnectorCenter(branchIndex);
                }
                else
                {
                    startPoint = sourceNode.GetOutputConnectorCenter();
                }
                
                _connectionPreviewLine.X1 = startPoint.X;
                _connectionPreviewLine.Y1 = startPoint.Y;
                _connectionPreviewLine.X2 = mousePos.X;
                _connectionPreviewLine.Y2 = mousePos.Y;
            }
        }
    }
    
    private void CanvasScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning && CanvasScrollViewer != null)
        {
            _isPanning = false;
            CanvasScrollViewer.ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }
    }
    
    private void WorkflowCanvas_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Delete:
                if (_selectedNode?.Step != null)
                {
                    SaveUndoState("Delete Node");
                    RemoveStep(_selectedNode.Step.StepId);
                    SelectNode(null);
                }
                break;
                
            case Key.Escape:
                if (_isConnecting)
                {
                    EndConnectionMode();
                }
                break;
                
            case Key.N when Keyboard.Modifiers == ModifierKeys.Control:
                SaveUndoState("Add Node");
                AddStep(WorkflowStepType.Standard);
                e.Handled = true;
                break;
                
            case Key.L when Keyboard.Modifiers == ModifierKeys.Control:
                SaveUndoState("Auto Layout");
                AutoLayout();
                e.Handled = true;
                break;
                
            case Key.D when Keyboard.Modifiers == ModifierKeys.Control:
                DuplicateSelectedNode();
                e.Handled = true;
                break;
                
            case Key.C when Keyboard.Modifiers == ModifierKeys.Control:
                CopySelectedNode();
                e.Handled = true;
                break;
                
            case Key.V when Keyboard.Modifiers == ModifierKeys.Control:
                PasteNode();
                e.Handled = true;
                break;
                
            case Key.Z when Keyboard.Modifiers == ModifierKeys.Control:
                Undo();
                e.Handled = true;
                break;
                
            case Key.Y when Keyboard.Modifiers == ModifierKeys.Control:
                Redo();
                e.Handled = true;
                break;
        }
    }
    
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        Undo();
    }
    
    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        Redo();
    }
    
    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        DuplicateSelectedNode();
    }
    
    private void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        if (Workflow == null || Workflow.Steps.Count == 0)
        {
            MessageBox.Show("No workflow to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            Title = "Export Workflow as PNG",
            FileName = $"{Workflow.Name ?? "workflow"}.png"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                ExportAsPng(dialog.FileName);
                MessageBox.Show($"Workflow exported to {dialog.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting workflow: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (Workflow == null || Workflow.Steps.Count == 0)
        {
            MessageBox.Show("No workflow to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var dialog = new SaveFileDialog
        {
            Filter = "JSON File|*.json",
            Title = "Export Workflow as JSON",
            FileName = $"{Workflow.Name ?? "workflow"}.json"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                ExportAsJson(dialog.FileName);
                MessageBox.Show($"Workflow exported to {dialog.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting workflow: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void Minimap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Workflow == null || Workflow.Steps.Count == 0) return;
        if (MinimapCanvas == null || Minimap == null || CanvasScrollViewer == null || CanvasScale == null) return;
        
        var pos = e.GetPosition(MinimapCanvas);
        var bounds = GetWorkflowBounds();
        var scale = Math.Min(
            (Minimap.Width - 20) / bounds.Width,
            (Minimap.Height - 30) / bounds.Height);
        scale = Math.Min(scale, MinimapScale * 2);
        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale)) return;
        
        // Convert minimap position to canvas position
        var canvasX = (pos.X - 5) / scale + bounds.X - CanvasScrollViewer.ViewportWidth / 2 / CanvasScale.ScaleX;
        var canvasY = (pos.Y - 20) / scale + bounds.Y - CanvasScrollViewer.ViewportHeight / 2 / CanvasScale.ScaleY;
        
        CanvasScrollViewer.ScrollToHorizontalOffset(canvasX * CanvasScale.ScaleX);
        CanvasScrollViewer.ScrollToVerticalOffset(canvasY * CanvasScale.ScaleY);
    }
    
    #endregion
}

/// <summary>
/// Represents a saved workflow state for undo/redo
/// </summary>
public class WorkflowState
{
    public string ActionName { get; set; } = "";
    public string StepsJson { get; set; } = "";
}
