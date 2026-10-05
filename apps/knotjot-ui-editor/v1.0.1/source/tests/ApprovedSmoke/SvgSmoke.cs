using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using KnotJotUiEditor;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

// Compare exported SVG through real WebView2 canvas pixels using an isolated browser profile.
// 使用隔离浏览器配置，通过真实 WebView2 画布像素比较导出 SVG。
static class SvgSmoke
{
    // Pump WPF only until asynchronous browser initialization and pixel checks finish.
    // 仅在浏览器异步初始化和像素检查结束前运行 WPF 消息循环。
    public static void Run(string output)
    {
        var completion=new TaskCompletionSource(); var task=completion.Task; var frame=new DispatcherFrame();
        Application.Current.Dispatcher.BeginInvoke(async ()=>{try{await RunAsync(output);completion.SetResult();}catch(Exception e){completion.SetException(e);}});
        task.ContinueWith(_=>Application.Current.Dispatcher.BeginInvoke(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();
    }
    // Render source-generated SVG and persist browser screenshots for alpha, luminance, stroke, and rotation cases.
    // 渲染源码生成 SVG，保留透明度、亮度、描边与旋转的浏览器截图。
    static async Task RunAsync(string output)
    {
        using var web=new WebView2{Width=100,Height=100};
        var window=new Window{Content=web,Width=120,Height=140,ShowInTaskbar=false,Left=-10000,Top=0};window.Show();
        try
        {
            await web.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(null,Path.Combine(output,"svg-profile")));
            var samples=new List<object>();
            foreach(var mode in new[]{"alpha","alpha-inverse","luminance","luminance-inverse"})
            foreach(var color in new[]{"#000000","#ffffff","#ff0000"})
            {
                var target=new ShapeElement{Kind="rect",X=0,Y=0,W=100,H=100,StrokeW=0,Fill="#ff0000",Opacity=.5};
                var mask=new ShapeElement{Kind="rect",X=0,Y=0,W=100,H=100,StrokeW=0,Fill=color,Opacity=.5,IsMask=true,MaskMode=mode,MaskTargetId=target.Id};
                double strength=.5*(mode.StartsWith("luminance")?(color=="#000000"?0:color=="#ffffff"?1:.2126):1);
                if(mode.EndsWith("inverse"))strength=1-strength;
                await Pixel(target,mask,50,50,strength*.5*255,mode+color.Substring(1));
            }
            var box=new ShapeElement{Kind="rect",X=0,Y=0,W=100,H=100,StrokeW=0,Fill="#ff0000"};
            var stripe=new ShapeElement{Kind="rect",X=40,Y=0,W=20,H=100,StrokeW=0,Rotation=90,Fill="#ffffff",IsMask=true,MaskTargetId=box.Id};
            await Pixel(box,stripe,10,50,255,"rotated-mask"); await Pixel(box,stripe,50,10,0,"rotated-mask-outside");
            stripe.Rotation=0;stripe.X=10;stripe.Y=10;stripe.W=80;stripe.H=80;stripe.NoFill=true;stripe.Stroke="#ffffff";stripe.StrokeW=10;stripe.MaskMode="luminance";
            await Pixel(box,stripe,50,50,0,"stroke-center");await Pixel(box,stripe,10,50,255,"stroke-edge");
            box.X=40;box.Y=0;box.W=20;box.H=100;box.Rotation=90;stripe.X=0;stripe.Y=0;stripe.W=100;stripe.H=100;stripe.NoFill=false;stripe.StrokeW=0;
            await Pixel(box,stripe,10,50,255,"rotated-target");
            File.WriteAllText(Path.Combine(output,"svg-pixels.json"),JsonSerializer.Serialize(samples,new JsonSerializerOptions{WriteIndented=true}));

            // Read actual canvas pixels after SVG image decoding, and enforce a small raster rounding tolerance.
            // 在 SVG 图片解码后读取实际画布像素，允许少量栅格舍入误差。
            async Task Pixel(ShapeElement target,ShapeElement mask,int x,int y,double expected,string name)
            {
                var method=typeof(CssBuilder).GetMethod("RenderDesignSvg",BindingFlags.Static|BindingFlags.NonPublic);
                var svg=(string)method.Invoke(null,new object[]{new List<CanvasElement>{target,mask},new Rect(0,0,100,100),DesignTargetLib.Find("card"),null,new ProjectExportSettings()});
                File.WriteAllText(Path.Combine(output,"svg-"+name+".svg"),svg);
                var html="<html><body style='margin:0;background:#ddd'><canvas id='c' width='100' height='100'></canvas><script>const i=new Image();i.onload=()=>{const ctx=c.getContext('2d');ctx.drawImage(i,0,0,100,100);window.pixel=ctx.getImageData("+x+","+y+",1,1).data[3];};i.src="+JsonSerializer.Serialize("data:image/svg+xml;base64,"+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg)))+";</script></body></html>";
                var loaded=new TaskCompletionSource(); EventHandler<CoreWebView2NavigationCompletedEventArgs> handler=(_,e)=>loaded.TrySetResult();web.CoreWebView2.NavigationCompleted+=handler;web.NavigateToString(html);await loaded.Task.WaitAsync(TimeSpan.FromSeconds(20));web.CoreWebView2.NavigationCompleted-=handler;
                string value="null";for(int attempt=0;attempt<100&&value=="null";attempt++){value=await web.ExecuteScriptAsync("window.pixel ?? null");if(value=="null")await Task.Delay(20);}
                int actual=JsonSerializer.Deserialize<int>(value);samples.Add(new{name,expected,actual});
                using(var file=File.Create(Path.Combine(output,"svg-"+name+".png")))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
                if(Math.Abs(actual-expected)>3)throw new Exception(name+" alpha="+actual+" expected="+expected);
            }
        }
        finally {window.Close();}
    }
}

