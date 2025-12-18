using PromptBox.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PromptBox.Controls;

/// <summary>
/// Visual node representing a workflow step on the canvas
/// </summary>
public partial class WorkflowNode : UserControl
{
    private Point _dragStartMousePosition;
    private Point _dragStartNodePosition;
    private bool _isDragging;
    private bool _isAtBoundary = false;
    
    /// <summary>
    /// Function to snap position to grid, provided by parent WorkflowCanvas
    /// </summary>
    public Func<Point, Point>? SnapToGridFunc { get; set; }
    
    /// <summary>
    /// Function to find alignment guides, provided by parent WorkflowCanvas
    /// </summary>
    public Func<Point, string, AlignmentResult>? FindAlignmentFunc { get; set; }
    
    /// <summary>
    /// Event raised when alignment guides should be shown/hidden
    /// </summary>
    public event EventHandler<AlignmentResult>? AlignmentChanged;
    
    #region Dependency Properties
    
    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(WorkflowStep), typeof(WorkflowNode),
            new PropertyMetadata(null, OnStepChanged));
    
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(WorkflowNode),
            new PropertyMetadata(false, OnIsSelectedChanged));
    
    public static readonly DependencyProperty IsExecutingProperty =
        DependencyProperty.Register(nameof(IsExecuting), typeof(bool), typeof(WorkflowNode),
            new PropertyMetadata(false, OnIsExecutingChanged));
    
    public static readonly DependencyProperty ExecutionStatusProperty =
        DependencyProperty.Register(nameof(ExecutionStatus), typeof(NodeExecutionStatus), typeof(WorkflowNode),
            new PropertyMetadata(NodeExecutionStatus.Pending, OnExecutionStatusChanged));
    
    public static readonly DependencyProperty StepNumberProperty =
        DependencyProperty.Register(nameof(StepNumber), typeof(int), typeof(WorkflowNode),
            new PropertyMetadata(1, OnStepNumberChanged));
    
    public WorkflowStep? Step
    {
        get => (WorkflowStep?)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }
    
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }
    
    public bool IsExecuting
    {
        get => (bool)GetValue(IsExecutingProperty);
        set => SetValue(IsExecutingProperty, value);
    }
    
    public NodeExecutionStatus ExecutionStatus
    {
        get => (NodeExecutionStatus)GetValue(ExecutionStatusProperty);
        set => SetValue(ExecutionStatusProperty, value);
    }
    
    public int StepNumber
    {
        get => (int)GetValue(StepNumberProperty);
        set => SetValue(StepNumberProperty, value);
    }
    
    #endregion
    
    #region Events
    
    public event EventHandler<WorkflowNode>? NodeSelected;
    public event EventHandler<Point>? NodeMoved;
    public event EventHandler<string>? InputConnectorClicked;
    public event EventHandler<string>? OutputConnectorClicked;
    public event EventHandler<string>? BranchConnectorClicked;
    public event EventHandler<string>? AddBranchRequested;
    
    #endregion
    
    public WorkflowNode()
    {
        InitializeComponent();
        
        // Ensure UI is updated after control is loaded
        Loaded += (s, e) =>
        {
            if (Step != null)
            {
                UpdateFromStep(Step);
            }
            UpdateExecutionStatus(ExecutionStatus);
        };
    }
    
    #region Property Changed Handlers
    
    private static void OnStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WorkflowNode node && e.NewValue is WorkflowStep step)
        {
            node.UpdateFromStep(step);
        }
    }
    
    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WorkflowNode node && node.SelectionBorder != null)
        {
            node.SelectionBorder.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    
    private static void OnIsExecutingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WorkflowNode node && node.ExecutionProgress != null)
        {
            node.ExecutionProgress.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    
    private static void OnExecutionStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WorkflowNode node)
        {
            node.UpdateExecutionStatus((NodeExecutionStatus)e.NewValue);
        }
    }
    
    private static void OnStepNumberChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WorkflowNode node && node.StepNumberText != null)
        {
            node.StepNumberText.Text = e.NewValue?.ToString() ?? "1";
        }
    }
    
    #endregion
    
    #region Update Methods
    
    private void UpdateFromStep(WorkflowStep step)
    {
        // Safety checks
        if (StepNameText == null || StepNumberText == null || PromptPreviewText == null) return;
        
        StepNameText.Text = step.Name ?? "Unnamed Step";
        StepNumberText.Text = (step.Order + 1).ToString();
        
        // Truncate prompt preview
        var preview = step.PromptTemplate ?? "";
        if (preview.Length > 100)
            preview = preview[..100] + "...";
        PromptPreviewText.Text = preview;
        
        // Update step type icon
        if (StepTypeIcon != null)
        {
            StepTypeIcon.Kind = step.StepType switch
            {
                WorkflowStepType.Conditional => MaterialDesignThemes.Wpf.PackIconKind.CallSplit,
                WorkflowStepType.Loop => MaterialDesignThemes.Wpf.PackIconKind.Repeat,
                WorkflowStepType.Parallel => MaterialDesignThemes.Wpf.PackIconKind.CallMerge,
                _ => MaterialDesignThemes.Wpf.PackIconKind.Play
            };
        }
        
        // Update header color based on step type
        if (HeaderBorder != null)
        {
            HeaderBorder.Background = step.StepType switch
            {
                WorkflowStepType.Conditional => new SolidColorBrush(Color.FromRgb(33, 150, 243)), // Blue
                WorkflowStepType.Loop => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                WorkflowStepType.Parallel => new SolidColorBrush(Color.FromRgb(156, 39, 176)), // Purple
                _ => (Brush)FindResource("PrimaryHueMidBrush")
            };
        }
        
        // Update connector visibility
        if (InputConnector != null)
            InputConnector.Visibility = step.IsStartStep ? Visibility.Collapsed : Visibility.Visible;
        if (OutputConnector != null)
            OutputConnector.Visibility = step.IsEndStep ? Visibility.Collapsed : Visibility.Visible;
        
        // Update branch connectors for conditional steps
        UpdateBranchConnectors(step);
    }
    
    private void UpdateBranchConnectors(WorkflowStep step)
    {
        if (BranchConnectors == null) return;
        BranchConnectors.Items.Clear();
        
        if (step.StepType == WorkflowStepType.Conditional)
        {
            // Show existing branches
            foreach (var branch in step.ConditionalBranches)
            {
                var connector = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, -6, 4) };
                
                var label = new TextBlock
                {
                    Text = branch.Label,
                    FontSize = 9,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243))
                };
                
                var ellipse = new System.Windows.Shapes.Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 1,
                    Cursor = Cursors.Hand,
                    ToolTip = $"Branch: {branch.Label}"
                };
                
                // Store branch label for connection creation
                var branchLabel = branch.Label;
                ellipse.MouseLeftButtonDown += (s, e) =>
                {
                    // Invoke with step ID to start connection mode from this branch
                    BranchConnectorClicked?.Invoke(this, branchLabel);
                    e.Handled = true;
                };
                
                connector.Children.Add(label);
                connector.Children.Add(ellipse);
                BranchConnectors.Items.Add(connector);
            }
            
            // Add "+" button to create new branch
            var addBranchPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, -6, 4) };
            
            var addLabel = new TextBlock
            {
                Text = "+",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80))
            };
            
            var addEllipse = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1,
                Cursor = Cursors.Hand,
                ToolTip = "Add new branch"
            };
            
            addEllipse.MouseLeftButtonDown += (s, e) =>
            {
                // Create a new branch with default label
                var newBranchLabel = $"Branch {step.ConditionalBranches.Count + 1}";
                AddBranchRequested?.Invoke(this, newBranchLabel);
                e.Handled = true;
            };
            
            addBranchPanel.Children.Add(addLabel);
            addBranchPanel.Children.Add(addEllipse);
            BranchConnectors.Items.Add(addBranchPanel);
        }
    }
    
    private void UpdateExecutionStatus(NodeExecutionStatus status)
    {
        // Safety check - UI elements may not be initialized yet
        if (StatusText == null || StatusIcon == null) return;
        
        var (text, icon, color) = status switch
        {
            NodeExecutionStatus.Running => ("Running...", MaterialDesignThemes.Wpf.PackIconKind.Loading, Colors.Orange),
            NodeExecutionStatus.Success => ("Completed", MaterialDesignThemes.Wpf.PackIconKind.CheckCircle, Colors.Green),
            NodeExecutionStatus.Failed => ("Failed", MaterialDesignThemes.Wpf.PackIconKind.AlertCircle, Colors.Red),
            NodeExecutionStatus.Skipped => ("Skipped", MaterialDesignThemes.Wpf.PackIconKind.SkipNext, Colors.Gray),
            _ => ("Ready", MaterialDesignThemes.Wpf.PackIconKind.Circle, Colors.Gray)
        };
        
        StatusText.Text = text;
        StatusIcon.Kind = icon;
        StatusIcon.Foreground = new SolidColorBrush(color);
    }
    
    #endregion
    
    #region Event Handlers
    
    private void NodeCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        
        // Get the canvas parent for accurate positioning
        var canvas = FindParentCanvas();
        _dragStartMousePosition = canvas != null ? e.GetPosition(canvas) : e.GetPosition(this);
        _dragStartNodePosition = Step?.Position ?? new Point(0, 0);
        
        NodeCard.CaptureMouse();
        NodeSelected?.Invoke(this, this);
        e.Handled = true;
    }
    
    private void NodeCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed && Step != null)
        {
            var canvas = FindParentCanvas();
            if (canvas == null) return;
            
            var currentMousePosition = e.GetPosition(canvas);
            
            // Bug 1 Fix: Account for zoom transform when calculating delta
            var scale = GetCanvasScale();
            var deltaX = (currentMousePosition.X - _dragStartMousePosition.X) / scale.ScaleX;
            var deltaY = (currentMousePosition.Y - _dragStartMousePosition.Y) / scale.ScaleY;
            
            // Enhancement 1: Calculate raw position before clamping
            var rawX = _dragStartNodePosition.X + deltaX;
            var rawY = _dragStartNodePosition.Y + deltaY;
            
            // Enhancement 1: Clamp position to canvas bounds with boundary detection
            var clampedX = Math.Max(0, Math.Min(2000 - NodeWidth, rawX));
            var clampedY = Math.Max(0, Math.Min(1500 - NodeHeight, rawY));
            
            // Enhancement 1: Check if at boundary and show visual feedback
            var atBoundary = rawX != clampedX || rawY != clampedY;
            if (atBoundary != _isAtBoundary)
            {
                _isAtBoundary = atBoundary;
                UpdateBoundaryFeedback(atBoundary);
            }
            
            var newPosition = new Point(clampedX, clampedY);
            
            // Enhancement 3: Check for alignment with other nodes
            if (FindAlignmentFunc != null)
            {
                var alignment = FindAlignmentFunc(newPosition, Step.StepId);
                if (alignment.HasAlignment)
                {
                    // Snap to alignment if within threshold
                    if (alignment.SnapToX.HasValue)
                        newPosition.X = alignment.SnapToX.Value;
                    if (alignment.SnapToY.HasValue)
                        newPosition.Y = alignment.SnapToY.Value;
                }
                AlignmentChanged?.Invoke(this, alignment);
            }
            
            // Bug 2 Fix: Apply snap to grid BEFORE setting position to avoid stuttering
            if (SnapToGridFunc != null)
            {
                newPosition = SnapToGridFunc(newPosition);
            }
            
            Step.Position = newPosition;
            Step.HasValidPosition = true; // Bug 5 Fix: Mark position as explicitly set
            Canvas.SetLeft(this, newPosition.X);
            Canvas.SetTop(this, newPosition.Y);
            
            NodeMoved?.Invoke(this, newPosition);
        }
    }
    
    /// <summary>
    /// Enhancement 1: Updates visual feedback when node is at canvas boundary
    /// </summary>
    private void UpdateBoundaryFeedback(bool atBoundary)
    {
        if (BoundaryWarningBorder != null)
        {
            BoundaryWarningBorder.Visibility = atBoundary ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    
    /// <summary>
    /// Gets the canvas scale transform from the parent WorkflowCanvas
    /// </summary>
    private ScaleTransform GetCanvasScale()
    {
        DependencyObject? parent = VisualTreeHelper.GetParent(this);
        while (parent != null)
        {
            if (parent is FrameworkElement element && element.Name == "CanvasContainer")
            {
                if (element.LayoutTransform is ScaleTransform scale)
                {
                    return scale;
                }
            }
            // Also check for WorkflowCanvas UserControl
            if (parent is UserControl uc && uc.GetType().Name == "WorkflowCanvas")
            {
                // Find the CanvasScale transform
                var canvasScale = uc.FindName("CanvasScale") as ScaleTransform;
                if (canvasScale != null)
                {
                    return canvasScale;
                }
            }
            parent = VisualTreeHelper.GetParent(parent);
        }
        return new ScaleTransform(1, 1); // Default scale if not found
    }
    
    private void NodeCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _isAtBoundary = false;
        UpdateBoundaryFeedback(false);
        
        // Clear alignment guides when drag ends
        AlignmentChanged?.Invoke(this, new AlignmentResult());
        
        NodeCard.ReleaseMouseCapture();
    }
    
    private Canvas? FindParentCanvas()
    {
        DependencyObject? parent = VisualTreeHelper.GetParent(this);
        while (parent != null)
        {
            if (parent is Canvas canvas)
                return canvas;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
    
    private void InputConnector_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        InputConnectorClicked?.Invoke(this, Step?.StepId ?? string.Empty);
        e.Handled = true;
    }
    
    private void OutputConnector_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OutputConnectorClicked?.Invoke(this, Step?.StepId ?? string.Empty);
        e.Handled = true;
    }
    
    #endregion
    
    #region Public Methods
    
    // Fixed node dimensions (matching XAML Width="200" MinHeight="100")
    private const double NodeWidth = 200;
    private const double NodeHeight = 112; // Approximate rendered height
    
    /// <summary>
    /// Gets the effective width of the node, with fallbacks for unmeasured state
    /// </summary>
    private double GetEffectiveWidth()
    {
        // Bug 4 Fix: Use fallback chain for width when ActualWidth is 0
        if (ActualWidth > 0) return ActualWidth;
        if (DesiredSize.Width > 0) return DesiredSize.Width;
        return NodeWidth;
    }
    
    /// <summary>
    /// Gets the effective height of the node, with fallbacks for unmeasured state
    /// </summary>
    private double GetEffectiveHeight()
    {
        // Bug 4 Fix: Use fallback chain for height when ActualHeight is 0
        if (ActualHeight > 0) return ActualHeight;
        if (DesiredSize.Height > 0) return DesiredSize.Height;
        return NodeHeight;
    }
    
    /// <summary>
    /// Gets the center point of the input connector (top center of node)
    /// </summary>
    public Point GetInputConnectorCenter()
    {
        var pos = Step?.Position ?? new Point(0, 0);
        var width = GetEffectiveWidth();
        // Input connector is at top center, no vertical offset needed
        // The connector ellipse has Margin="0,-6,0,0" but that's visual only
        return new Point(pos.X + width / 2, pos.Y);
    }
    
    /// <summary>
    /// Gets the center point of the output connector (bottom center of node)
    /// </summary>
    public Point GetOutputConnectorCenter()
    {
        var pos = Step?.Position ?? new Point(0, 0);
        var width = GetEffectiveWidth();
        var height = GetEffectiveHeight();
        // Output connector is at bottom center
        // The connector ellipse has Margin="0,0,0,-6" but that's visual only
        return new Point(pos.X + width / 2, pos.Y + height);
    }
    
    /// <summary>
    /// Gets the center point of a branch connector on the right side of the node
    /// </summary>
    public Point GetBranchConnectorCenter(int branchIndex)
    {
        var pos = Step?.Position ?? new Point(0, 0);
        var width = GetEffectiveWidth();
        var height = GetEffectiveHeight();
        
        // Branch connectors are in a vertically centered ItemsControl on the right side
        // Each branch item has ~16px height (10px ellipse + 4px top margin + 4px bottom margin - some overlap)
        // The ItemsControl has Margin="0,20,0,20" and VerticalAlignment="Center"
        
        var branchCount = Step?.ConditionalBranches.Count ?? 0;
        if (branchCount == 0) branchCount = 1;
        
        // Calculate the total height of all branch items (including the + button)
        const double itemHeight = 18; // Approximate height per branch item
        var totalBranchHeight = (branchCount + 1) * itemHeight; // +1 for the "+" button
        
        // The branches are centered vertically in the node
        var branchStartY = (height - totalBranchHeight) / 2;
        var branchY = branchStartY + (branchIndex * itemHeight) + (itemHeight / 2);
        
        return new Point(pos.X + width, pos.Y + branchY);
    }
    
    /// <summary>
    /// Gets the center point of the left side of the node (for incoming horizontal connections)
    /// </summary>
    public Point GetLeftConnectorCenter()
    {
        var pos = Step?.Position ?? new Point(0, 0);
        var height = GetEffectiveHeight();
        return new Point(pos.X, pos.Y + height / 2);
    }
    
    #endregion
}

/// <summary>
/// Result of alignment detection for grid alignment guides
/// </summary>
public class AlignmentResult
{
    /// <summary>Whether any alignment was found</summary>
    public bool HasAlignment => HorizontalLines.Count > 0 || VerticalLines.Count > 0;
    
    /// <summary>X position to snap to (if aligned horizontally)</summary>
    public double? SnapToX { get; set; }
    
    /// <summary>Y position to snap to (if aligned vertically)</summary>
    public double? SnapToY { get; set; }
    
    /// <summary>Horizontal alignment lines to draw (Y coordinates)</summary>
    public List<double> HorizontalLines { get; set; } = new();
    
    /// <summary>Vertical alignment lines to draw (X coordinates)</summary>
    public List<double> VerticalLines { get; set; } = new();
}
