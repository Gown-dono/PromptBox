using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace PromptBox.Controls;

/// <summary>
/// Type of connector between workflow nodes
/// </summary>
public enum ConnectorType
{
    Standard,
    Conditional,
    Loop,
    Error
}

/// <summary>
/// Visual connector (bezier curve with animated flow indicators) between workflow nodes
/// </summary>
public class WorkflowConnector : Canvas
{
    #region Dependency Properties
    
    public static readonly DependencyProperty StartPointProperty =
        DependencyProperty.Register(nameof(StartPoint), typeof(Point), typeof(WorkflowConnector),
            new PropertyMetadata(new Point(0, 0), OnPointsChanged));
    
    public static readonly DependencyProperty EndPointProperty =
        DependencyProperty.Register(nameof(EndPoint), typeof(Point), typeof(WorkflowConnector),
            new PropertyMetadata(new Point(0, 0), OnPointsChanged));
    
    public static readonly DependencyProperty ConnectorTypeProperty =
        DependencyProperty.Register(nameof(ConnectorType), typeof(ConnectorType), typeof(WorkflowConnector),
            new PropertyMetadata(ConnectorType.Standard, OnConnectorTypeChanged));
    
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(WorkflowConnector),
            new PropertyMetadata(string.Empty));
    
    public static readonly DependencyProperty FromStepIdProperty =
        DependencyProperty.Register(nameof(FromStepId), typeof(string), typeof(WorkflowConnector),
            new PropertyMetadata(string.Empty));
    
    public static readonly DependencyProperty ToStepIdProperty =
        DependencyProperty.Register(nameof(ToStepId), typeof(string), typeof(WorkflowConnector),
            new PropertyMetadata(string.Empty));
    
    public Point StartPoint
    {
        get => (Point)GetValue(StartPointProperty);
        set => SetValue(StartPointProperty, value);
    }
    
    public Point EndPoint
    {
        get => (Point)GetValue(EndPointProperty);
        set => SetValue(EndPointProperty, value);
    }
    
    public ConnectorType ConnectorType
    {
        get => (ConnectorType)GetValue(ConnectorTypeProperty);
        set => SetValue(ConnectorTypeProperty, value);
    }
    
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
    
    public string FromStepId
    {
        get => (string)GetValue(FromStepIdProperty);
        set => SetValue(FromStepIdProperty, value);
    }
    
    public string ToStepId
    {
        get => (string)GetValue(ToStepIdProperty);
        set => SetValue(ToStepIdProperty, value);
    }
    
    #endregion
    
    private Path? _pathElement;
    private readonly Ellipse[] _flowIndicators = new Ellipse[3];
    private Canvas? _flowIndicatorsCanvas;
    
    /// <summary>
    /// Sets the canvas where flow indicators should be drawn (above nodes)
    /// </summary>
    public Canvas? FlowIndicatorsCanvas
    {
        get => _flowIndicatorsCanvas;
        set
        {
            // Remove indicators from old canvas
            if (_flowIndicatorsCanvas != null)
            {
                foreach (var indicator in _flowIndicators)
                {
                    if (indicator != null && _flowIndicatorsCanvas.Children.Contains(indicator))
                    {
                        _flowIndicatorsCanvas.Children.Remove(indicator);
                    }
                }
            }
            
            _flowIndicatorsCanvas = value;
            
            // Add indicators to new canvas
            if (_flowIndicatorsCanvas != null)
            {
                foreach (var indicator in _flowIndicators)
                {
                    if (indicator != null && !_flowIndicatorsCanvas.Children.Contains(indicator))
                    {
                        _flowIndicatorsCanvas.Children.Add(indicator);
                    }
                }
            }
        }
    }
    
    public WorkflowConnector()
    {
        CreateVisualElements();
    }
    
    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WorkflowConnector connector)
        {
            connector.UpdateConnector();
        }
    }
    
    private static void OnConnectorTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WorkflowConnector connector)
        {
            connector.UpdateStroke();
            connector.UpdateConnector();
        }
    }
    
    private void CreateVisualElements()
    {
        // Create the path for the connector line
        _pathElement = new Path
        {
            StrokeThickness = 2,
            Fill = null
        };
        Children.Add(_pathElement);
        
        // Create 3 animated flow indicators (circles that move along the path)
        // Note: These will be added to FlowIndicatorsCanvas when it's set
        for (int i = 0; i < 3; i++)
        {
            _flowIndicators[i] = new Ellipse
            {
                Width = 6,
                Height = 6,
                Opacity = 0.7
            };
        }
        
        UpdateStroke();
    }
    
    private void UpdateConnector()
    {
        if (_pathElement == null) return;
        
        var start = StartPoint;
        var end = EndPoint;
        
        // Calculate control points for smooth bezier curve
        var deltaX = Math.Abs(end.X - start.X);
        var deltaY = Math.Abs(end.Y - start.Y);
        
        Point controlPoint1, controlPoint2;
        
        // Determine if this is primarily horizontal or vertical
        if (deltaX > deltaY * 1.5)
        {
            // Horizontal connection - use horizontal bezier
            var midX = (start.X + end.X) / 2;
            controlPoint1 = new Point(midX, start.Y);
            controlPoint2 = new Point(midX, end.Y);
        }
        else
        {
            // Vertical or diagonal connection - use vertical bezier
            var midY = (start.Y + end.Y) / 2;
            controlPoint1 = new Point(start.X, midY);
            controlPoint2 = new Point(end.X, midY);
        }
        
        // Create the bezier path geometry
        var pathGeometry = new PathGeometry();
        var pathFigure = new PathFigure { StartPoint = start };
        pathFigure.Segments.Add(new BezierSegment(controlPoint1, controlPoint2, end, true));
        pathGeometry.Figures.Add(pathFigure);
        
        _pathElement.Data = pathGeometry;
        
        // Animate flow indicators along the path
        AnimateFlowIndicators(pathGeometry);
    }
    
    private void AnimateFlowIndicators(PathGeometry pathGeometry)
    {
        const double indicatorSize = 6;
        const double offset = indicatorSize / 2; // Center the indicator on the path
        
        // Create animated circles that move along the path to show direction
        for (int i = 0; i < _flowIndicators.Length; i++)
        {
            var indicator = _flowIndicators[i];
            
            // IMPORTANT: Stop any existing animations first to prevent lingering dots
            indicator.BeginAnimation(Canvas.LeftProperty, null);
            indicator.BeginAnimation(Canvas.TopProperty, null);
            
            // Create animation along the path with offset to center the indicator
            var animation = new DoubleAnimationUsingPath
            {
                PathGeometry = pathGeometry,
                Source = PathAnimationSource.X,
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(i * 666) // Stagger the indicators
            };
            
            var animationY = new DoubleAnimationUsingPath
            {
                PathGeometry = pathGeometry,
                Source = PathAnimationSource.Y,
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(i * 666)
            };
            
            // Create a transform group to offset the indicator to center it on the path
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new TranslateTransform(-offset, -offset));
            indicator.RenderTransform = transformGroup;
            
            // Apply animations to move the indicator along the path
            indicator.BeginAnimation(Canvas.LeftProperty, animation);
            indicator.BeginAnimation(Canvas.TopProperty, animationY);
        }
    }
    
    private void UpdateStroke()
    {
        if (_pathElement == null) return;
        
        var brush = ConnectorType switch
        {
            ConnectorType.Conditional => new SolidColorBrush(Color.FromRgb(33, 150, 243)), // Blue
            ConnectorType.Loop => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
            ConnectorType.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
            _ => new SolidColorBrush(Color.FromRgb(158, 158, 158)) // Gray
        };
        
        _pathElement.Stroke = brush;
        
        // Update flow indicator colors
        foreach (var indicator in _flowIndicators)
        {
            if (indicator != null)
            {
                indicator.Fill = brush;
            }
        }
    }
    
    /// <summary>
    /// Cleans up flow indicators when connector is removed
    /// </summary>
    public void Cleanup()
    {
        // Stop all animations
        foreach (var indicator in _flowIndicators)
        {
            if (indicator != null)
            {
                indicator.BeginAnimation(Canvas.LeftProperty, null);
                indicator.BeginAnimation(Canvas.TopProperty, null);
            }
        }
        
        // Remove indicators from canvas
        if (_flowIndicatorsCanvas != null)
        {
            foreach (var indicator in _flowIndicators)
            {
                if (indicator != null && _flowIndicatorsCanvas.Children.Contains(indicator))
                {
                    _flowIndicatorsCanvas.Children.Remove(indicator);
                }
            }
        }
    }
}
