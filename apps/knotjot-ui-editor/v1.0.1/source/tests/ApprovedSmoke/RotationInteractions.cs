using System.Reflection;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KnotJotUiEditor;

// Assert canvas-space selection and drag results through the production interaction controller.
// 通过生产交互控制器断言画布坐标中的选择与拖拽结果。
static class RotationInteractions
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    // Invoke the same coordinate handler used by pointer events, without moving the user's cursor.
    // 调用指针事件共用的坐标处理函数，不移动用户鼠标。
    static object Call(object o,string n,params object[] args){try{return o.GetType().GetMethods(Flags).First(m=>m.Name==n&&m.GetParameters().Length==args.Length).Invoke(o,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    // Read existing selection visuals and gesture state for independent coordinate assertions.
    // 读取已有选择视图和手势状态，进行独立坐标断言。
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    // Require a point to match a separately calculated canvas coordinate.
    // 要求点匹配独立计算的画布坐标。
    static void At(Point actual,Point expected,string name){if((actual-expected).Length>.01)throw new Exception(name+" actual="+actual+" expected="+expected);}
    // Initialize a selected fixture without showing a browser or writing user state.
    // 初始化选中样本，不显示浏览器或写入用户状态。
    static MainWindow Window(CanvasElement item){var w=new MainWindow();Call(w,"InitAdorners");Call(w,"InitDesign");Call(w,"SwitchTarget","card",true);Call(w,"AddElementSilent",item,-1);Call(w,"RebuildZ");Call(w,"SelectElement",item,true);return w;}
    // Read a handle's actual canvas center after selection synchronization.
    // 在选择同步后读取手柄的实际画布中心。
    static Point Center(FrameworkElement h)=>new(Canvas.GetLeft(h)+h.Width/2,Canvas.GetTop(h)+h.Height/2);
    // Rotate an endpoint using independent trigonometry rather than the production transform helper.
    // 使用独立三角函数旋转端点，不依赖生产变换辅助函数。
    static Point VisibleEnd(ShapeElement s,bool first){var c=new Point((s.X+s.X2)/2,(s.Y+s.Y2)/2);var p=new Point(first?s.X:s.X2,first?s.Y:s.Y2)-c;var a=s.Rotation*Math.PI/180;return c+new Vector(p.X*Math.Cos(a)-p.Y*Math.Sin(a),p.X*Math.Sin(a)+p.Y*Math.Cos(a));}
    // Verify the rotated outline, resize direction, opposite-edge anchor, and updated handle position.
    // 验证旋转选框、缩放方向、对边固定及更新后手柄位置。
    public static void Shape(string output)
    {
        var s=new ShapeElement{Kind="rect",X=240,Y=200,W=20,H=100,Rotation=90,StrokeW=0};var w=Window(s);
        var outline=Field<Rectangle>(w,"_outline");var local=outline.RenderTransform.Transform(new Point(0,0));
        At(new Point(Canvas.GetLeft(outline)+local.X,Canvas.GetTop(outline)+local.Y),new Point(303,237),"Outline top-left");
        var handle=Field<Rectangle[]>(w,"_handles")[3];At(Center(handle),new Point(250,263),"Rotated right handle");
        Call(w,"BeginHandleDrag",3,Center(handle));Call(w,"ApplyDragPoint",new Point(250,283));
        if(Math.Abs(s.W-40)>.01||Math.Abs(s.H-100)>.01)throw new Exception("Resize must follow local width axis");
        At(new Point(s.X+s.W/2,s.Y+s.H/2),new Point(250,260),"Resized center");At(Center(handle),new Point(250,283),"Handle follows pointer");
        Capture(w,output,"rotated-shape-selection");
        Call(w,"Undo");var restored=Field<List<CanvasElement>>(w,"_elements").OfType<ShapeElement>().Single();if(restored.W!=20||restored.X!=240)throw new Exception("Rotated resize undo");
    }
    // Verify rotated endpoint hit geometry, endpoint dragging, fixed opposite end, and endpoint handle refresh.
    // 验证旋转端点命中、端点拖动、对端固定及端点手柄刷新。
    public static void Line(string output)
    {
        var s=new ShapeElement{Kind="line",X=200,Y=200,X2=200,Y2=300,Rotation=90,StrokeW=2};var w=Window(s);
        var ends=Field<Rectangle[]>(w,"_ends");At(Center(ends[0]),new Point(250,250),"Visible first endpoint");At(Center(ends[1]),new Point(150,250),"Visible second endpoint");
        var hit=Field<Dictionary<ShapeElement,Line>>(w,"_lineHits")[s];At(hit.RenderTransform.Transform(new Point(hit.X1,hit.Y1)),new Point(250,250),"Hit strip endpoint");
        var stage=(Canvas)w.FindName("Stage");stage.Measure(new Size(1200,700));stage.Arrange(new Rect(0,0,1200,700));stage.UpdateLayout();
        var actualHit = VisualTreeHelper.HitTest(stage,new Point(240,250));
        if(actualHit?.VisualHit != hit)throw new Exception("Rendered line hit test did not hit its separate strip");
        if(VisualTreeHelper.HitTest(stage,new Point(200,210))?.VisualHit == hit)throw new Exception("Old invisible strip remains hit-testable");
        var geometry=new LineGeometry(new Point(hit.X1,hit.Y1),new Point(hit.X2,hit.Y2)){Transform=hit.RenderTransform};
        if(!geometry.StrokeContains(new Pen(Brushes.Black,hit.StrokeThickness),new Point(240,250))||geometry.StrokeContains(new Pen(Brushes.Black,hit.StrokeThickness),new Point(200,210)))throw new Exception("Hit strip remains at old position");
        Call(w,"BeginHandleDrag",100,Center(ends[0]));Call(w,"ApplyDragPoint",new Point(270,260));
        At(VisibleEnd(s,true),new Point(270,260),"Dragged endpoint");At(VisibleEnd(s,false),new Point(150,250),"Fixed opposite endpoint");At(Center(ends[0]),new Point(270,260),"Endpoint handle follows pointer");
        Call(w,"BeginHandleDrag",101,Center(ends[1]));Call(w,"ApplyDragPoint",new Point(130,230));
        At(VisibleEnd(s,true),new Point(270,260),"First endpoint fixed during second-end drag");At(VisibleEnd(s,false),new Point(130,230),"Second endpoint follows pointer");
        Capture(w,output,"rotated-line-selection");
    }
    // Retain the actual artwork and overlay together so selection alignment can be visually reviewed.
    // 同时保留实际图稿和叠加层，供目视审查选择对齐。
    static void Capture(MainWindow w,string output,string name)
    {
        var bmp=new RenderTargetBitmap(400,400,96,96,PixelFormats.Pbgra32);
        foreach(var id in new[]{"Stage","Overlay"}){var canvas=(Canvas)w.FindName(id);canvas.InvalidateMeasure();canvas.InvalidateArrange();canvas.Measure(new Size(1200,700));canvas.Arrange(new Rect(0,0,1200,700));canvas.UpdateLayout();bmp.Render(canvas);}
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(System.IO.Path.Combine(output,name+".png"));png.Save(file);
    }

    // Exercise non-right-angle corner dragging and the shared image/ink selection path.
    // 验证非直角旋转的角点拖动，以及图片和手绘共用选择路径。
    public static void AngledBoxes()
    {
        var s=new ShapeElement{Kind="rect",X=200,Y=200,W=80,H=40,Rotation=30,StrokeW=0};var w=Window(s);
        var handle=Field<Rectangle[]>(w,"_handles")[4];var start=Center(handle);double a=Math.PI/6;
        var delta=new Vector(20*Math.Cos(a)-10*Math.Sin(a),20*Math.Sin(a)+10*Math.Cos(a));
        Call(w,"BeginHandleDrag",4,start);Call(w,"ApplyDragPoint",start+delta);Call(w,"ApplyDragPoint",start+delta*2);
        if(Math.Abs(s.W-120)>.01||Math.Abs(s.H-60)>.01)throw new Exception("Angled resize used canvas axes or accumulated origin");At(Center(handle),start+delta*2,"Angled corner follows pointer");
        var im=new ImageElement{X=240,Y=200,W=20,H=100,Rotation=90};var iw=Window(im);var ih=Field<Rectangle[]>(iw,"_handles")[3];At(Center(ih),new Point(250,263),"Image handle");Call(iw,"BeginHandleDrag",3,Center(ih));Call(iw,"ApplyDragPoint",new Point(250,283));if(Math.Abs(im.W-40)>.01)throw new Exception("Rotated image resize");
        var ink=new InkElement{Points=new List<Point>{new(240,200),new(260,300)},Rotation=90};var kw=Window(ink);var outline=Field<Rectangle>(kw,"_outline");if(outline.RenderTransform is not RotateTransform rotation||rotation.Angle!=90)throw new Exception("Ink outline rotation missing");
    }

    // Keep rotated group bounds and handles aligned during proportional canvas-space resizing.
    // 在画布坐标等比缩放期间保持旋转组合边界与手柄对齐。
    public static void Group()
    {
        var first=new ShapeElement{Kind="rect",X=240,Y=200,W=20,H=100,Rotation=90};var second=new ShapeElement{Kind="rect",X=320,Y=240,W=20,H=20};var w=Window(first);Call(w,"AddElementSilent",second,-1);Call(w,"SetSelection",new CanvasElement[]{first,second},first,true);
        var handle=Field<Rectangle[]>(w,"_handles")[4];At(Center(handle),new Point(343,263),"Rotated group bounds");Call(w,"BeginHandleDrag",4,Center(handle));Call(w,"ApplyDragPoint",new Point(483,283));
        At(Center(handle),new Point(483,283),"Group handle follows pointer");if(Math.Abs(first.W-40)>.01||Math.Abs(first.H-200)>.01||Math.Abs(second.W-40)>.01)throw new Exception("Rotated group scale is not proportional");
    }

}






