using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KnotJotUiEditor;

// Exercise approved repairs through production handlers and rendered WPF pixels in an isolated profile.
// 使用隔离配置，通过实际处理函数和 WPF 渲染像素验证获批修复。
static class Program
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static string output;
    static int failures;
    // Invoke an existing handler and expose its original failure for assertions.
    // 调用已有处理函数，向断言暴露原始失败。
    static object Call(object o, string n, params object[] args) { try { return o.GetType().GetMethods(Flags).First(m => m.Name == n && m.GetParameters().Length == args.Length).Invoke(o, args); } catch(TargetInvocationException e) { throw e.InnerException; } }
    // Read existing document state without adding production test hooks.
    // 读取已有文档状态，不增加生产测试入口。
    static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, Flags).GetValue(o);
    // Fail a named behavioral invariant.
    // 对具名行为约束报告失败。
    static void Need(bool v, string message) { if (!v) throw new Exception(message); }
    // Require rejection before accepting a resource boundary.
    // 要求在资源边界处拒绝输入。
    static void Reject(Action a) { try { a(); } catch (InvalidDataException) { return; } throw new Exception("Expected InvalidDataException"); }
    // Retain each independent result even when another regression fails.
    // 即使其他回归失败，也保留各项独立结果。
    static void Check(string name, Action a) { try { a(); Console.WriteLine("PASS " + name); } catch(Exception e) { failures++; Console.WriteLine("FAIL " + name + " " + e.Message); } }
    // Initialize editing controls without loading a browser or touching user preferences.
    // 初始化编辑控件，不加载浏览器或访问用户配置。
    static MainWindow Window() { var w = new MainWindow(); foreach(var n in new[]{"InitAdorners","InitDesign","WireShapePanel","WireInkImagePanels","WireTextPanel"}) Call(w,n); Call(w,"SwitchTarget","card",true); return w; }
    // Add a fixture and select it via the production selection path.
    // 添加样本并通过实际选择路径选中。
    static void Add(MainWindow w, CanvasElement el) { Call(w,"AddElementSilent",el,-1); Call(w,"SelectElement",el,true); }
    // Render the target visual, retain a PNG, and inspect its premultiplied center alpha.
    // 渲染目标视图，保留 PNG 并读取预乘中心透明度。
    static byte Render(MainWindow w, CanvasElement el, string name, int x=50, int y=50)
    {
        Call(w,"ApplyMasks"); var v=(FrameworkElement)Field<Dictionary<CanvasElement,UIElement>>(w,"_views")[el];
        v.Measure(new Size(100,100)); v.Arrange(new Rect(0,0,100,100)); v.UpdateLayout();
        var bmp=new RenderTargetBitmap(100,100,96,96,PixelFormats.Pbgra32); bmp.Render(v);
        var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp)); using(var f=File.Create(Path.Combine(output,name+".png"))) png.Save(f);
        var pixels=new byte[40000]; bmp.CopyPixels(pixels,400,0); return pixels[(y*100+x)*4+3];
    }
    // Execute focused UI, persistence, and history regressions; process status reports any failure.
    // 执行界面、持久化和历史回归，进程状态报告任何失败。
    [STAThread] static int Main(string[] args)
    {
        output=Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("KNOTJOT_UI_EDITOR_DATA_DIR",Path.Combine(output,"profile"));
        Environment.SetEnvironmentVariable("THOUGHTCANVAS_UI_SKINS_FILE",Path.Combine(output,"missing.json"));
        _=new Application();
        Check("U01-locked-size-40-not-208",()=>{var w=Window();var s=new ShapeElement{Kind="rect",W=40,H=30,IsLocked=true};Add(w,s);((TextBox)w.FindName("TxtSizeW")).Text="80";Call(w,"ApplySizeFields");Need(s.W==40,"Width="+s.W);});
        Check("U01-locked-properties-and-unlock",()=>{var w=Window();var s=new ShapeElement{Kind="rect",Fill="#123456",IsLocked=true};Add(w,s);((TextBox)w.FindName("TxtFill")).Text="#ffffff";Need(s.Fill=="#123456","Locked fill mutated");Call(w,"ToggleSelectedLock");Need(!s.IsLocked,"Unlock blocked");((TextBox)w.FindName("TxtFill")).Text="#abcdef";Need(s.Fill=="#abcdef","Unlocked edit blocked");});
        foreach(var mode in new[]{"alpha","alpha-inverse","luminance","luminance-inverse"}) foreach(var color in new[]{"#000000","#ffffff"}) foreach(var opacity in new[]{1d,.5d})
        Check("U02-"+mode+color+opacity,()=>{var w=Window();var s=new ShapeElement{Kind="rect",X=0,Y=0,W=100,H=100,StrokeW=0,Fill="#ff0000"};Add(w,s);var m=new ShapeElement{Kind="rect",X=0,Y=0,W=100,H=100,IsMask=true,MaskTargetId=s.Id,MaskMode=mode,Fill=color,Opacity=opacity};Add(w,m);double strength=opacity*(mode.StartsWith("luminance")&&color=="#000000"?0:1);if(mode.EndsWith("inverse"))strength=1-strength;var a=Render(w,s,mode+color.Substring(1)+opacity);Need(Math.Abs(a-strength*255)<3,"alpha="+a+" expected="+strength*255);});
        Check("U03-capacity-and-original",()=>{var file=Path.Combine(output,"ui-skins.json");var lib=new KnotJotSkinLibrary();using var rec=JsonDocument.Parse("{\"css\":\"\"}");for(int i=0;i<500;i++)lib.Skins["item-"+i]=rec.RootElement.Clone();File.WriteAllText(file,JsonSerializer.Serialize(lib));Environment.SetEnvironmentVariable("KNOTJOT_UI_SKINS_FILE",file);var before=File.ReadAllBytes(file);var target=Path.GetFullPath("apps/knotjot-ui-editor/v1.0.1/source/tests/CoreSmoke/fixtures/main.js");Reject(()=>KnotJotConnection.Write(target,"new-id","new","body{}",null,null));Need(before.SequenceEqual(File.ReadAllBytes(file)),"Original modified");KnotJotConnection.Write(target,"item-0","replace","body{}",null,null);using var doc=JsonDocument.Parse(File.ReadAllText(file));Need(doc.RootElement.GetProperty("skins").EnumerateObject().Count()==500,"Replacement count");});
        Check("U04-large-css-roundtrip",()=>{var file=Path.Combine(output,"css.knotjot-ui");File.WriteAllText(file,JsonSerializer.Serialize(new{css=new string('x',110000)}));using var doc=ProjectFileService.Open(file);Need(doc.RootElement.GetProperty("css").GetString().Length==110000,"CSS changed");});
        Check("O02-target-history",()=>{var w=Window();var s=new ShapeElement{Kind="rect",W=40,H=30};Add(w,s);Call(w,"PushUndo",new object[]{null});s.W=75;Call(w,"SwitchTarget","cardSel",false);Call(w,"Undo");Need(Field<string>(w,"_activeTarget")=="card","Wrong undo target");Need(Field<List<CanvasElement>>(w,"_elements").OfType<ShapeElement>().Single().W==40,"Undo width");Need(Field<CanvasElement>(w,"_sel")?.Id==s.Id,"Undo selection");Call(w,"Redo");Call(w,"SwitchTarget","card",false);Need(Field<List<CanvasElement>>(w,"_elements").OfType<ShapeElement>().Single().W==75,"Redo width");});
        Check("O04-existing-region-does-not-add-history",()=>{var w=Window();int count=Field<List<CanvasElement>>(w,"_elements").Count;Call(w,"AddTextRegionTool");Need(Field<List<CanvasElement>>(w,"_elements").Count==count,"Duplicate region");Need(Field<List<string>>(w,"_undoStack").Count==0,"Selection created undo");Need(((Button)w.FindName("BtnAddText")).ToolTip?.ToString().Contains("选中已有") == true,"Tooltip missing semantics");});
        Check("U03-final-byte-limit-original",()=>{
            var file=Path.Combine(output,"ui-skins.json");var lib=new KnotJotSkinLibrary();using var rec=JsonDocument.Parse(JsonSerializer.Serialize(new { css=new string('x',7*1024*1024) }));lib.Skins["large-one"]=rec.RootElement.Clone();lib.Skins["large-two"]=rec.RootElement.Clone();File.WriteAllText(file,JsonSerializer.Serialize(lib));Environment.SetEnvironmentVariable("KNOTJOT_UI_SKINS_FILE",file);var before=File.ReadAllBytes(file);
            Reject(()=>KnotJotConnection.Write(Path.GetFullPath("apps/knotjot-ui-editor/v1.0.1/source/tests/CoreSmoke/fixtures/main.js"),"third","third",new string('x',1024*1024),JsonSerializer.Serialize(new{css=new string('x',3*1024*1024)}),null));Need(before.SequenceEqual(File.ReadAllBytes(file)),"Oversize write modified original");
        });
        Check("U04-1001-elements-no-write",()=>{var w=Window();var els=Field<List<CanvasElement>>(w,"_elements");els.Clear();for(int i=0;i<1001;i++)els.Add(new ShapeElement{Kind="rect",X=550,Y=300,W=10,H=10});var file=Path.Combine(output,"original.knotjot-ui");File.WriteAllText(file,"original sentinel");w.GetType().GetField("_currentPath",Flags).SetValue(w,file);Need(!(bool)Call(w,"Save",false),"Oversize saved");Need(File.ReadAllText(file)=="original sentinel","Original changed");Need(els.Count==1001,"Validation silently truncated");});
        Check("U04-image-encoded-boundaries",()=>{var valid=new string('A',ProjectFileService.MaxImageChars);using(var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{base64=valid})))ProjectFileService.Validate(doc.RootElement);foreach(var bad in new[]{valid+"AAAA","data:image/png;base64,AAAA","A===","AA A","AB=="})Reject(()=>{using var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{base64=bad}));ProjectFileService.Validate(doc.RootElement);});});
        Check("U04-total-payload-bytes",()=>{var method=typeof(ProjectFileService).GetMethod("ValidatePayload",Flags);Need(method!=null,"No final payload validator");var json=JsonSerializer.Serialize(new{padding=Enumerable.Repeat(new string('x',90000),380).ToArray()});Reject(()=>{try{method.Invoke(null,new object[]{json});}catch(TargetInvocationException e){throw e.InnerException;}});});
        Check("U04-import-total-before-mutation",()=>{var w=Window();var image=new ImageElement{Base64=new string('A',ProjectFileService.MaxImageChars),Mime="image/png",W=100,H=100};int count=Field<List<CanvasElement>>(w,"_elements").Count;Reject(()=>Call(w,"ValidateElementAddition",image));Need(Field<List<CanvasElement>>(w,"_elements").Count==count,"Rejected import changed model");});
        Check("O02-new-resets-history",()=>{var w=Window();Call(w,"PushUndo",new object[]{null});Call(w,"NewDoc");Need(Field<List<string>>(w,"_undoStack").Count==0,"New retained undo");Need(Field<List<string>>(w,"_redoStack").Count==0,"New retained redo");});
        Check("O04-add-region-undo-redo",()=>{var w=Window();Call(w,"ClearElements");Call(w,"ResetUndo");Call(w,"AddTextRegionTool");Need(Field<List<CanvasElement>>(w,"_elements").OfType<TextRegionElement>().Count()==1,"Region missing");Call(w,"Undo");Need(!Field<List<CanvasElement>>(w,"_elements").OfType<TextRegionElement>().Any(),"Undo did not remove");Call(w,"Redo");Need(Field<List<CanvasElement>>(w,"_elements").OfType<TextRegionElement>().Count()==1,"Redo did not restore");});
        Check("U02-image-target-alpha",()=>{var raw=BitmapSource.Create(2,2,96,96,PixelFormats.Pbgra32,null,new byte[]{0,0,128,128,0,0,128,128,0,0,128,128,0,0,128,128},8);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(raw));using var stream=new MemoryStream();encoder.Save(stream);var w=Window();var im=new ImageElement{X=0,Y=0,W=100,H=100,Opacity=.5,Base64=Convert.ToBase64String(stream.ToArray()),Mime="image/png"};Add(w,im);var mask=new ShapeElement{Kind="rect",X=0,Y=0,W=100,H=100,StrokeW=0,IsMask=true,MaskTargetId=im.Id,Fill="#ffffff",Opacity=.5,MaskMode="luminance"};Add(w,mask);Need(Math.Abs(Render(w,im,"image-half-target-half-mask")-32)<3,"Image alpha was not multiplied");});
        Check("U02-mask-rotation",()=>{var w=Window();var s=new ShapeElement{Kind="rect",W=100,H=100,StrokeW=0};Add(w,s);var m=new ShapeElement{Kind="rect",X=40,Y=0,W=20,H=100,StrokeW=0,Fill="#ffffff",Rotation=90,IsMask=true,MaskTargetId=s.Id};Add(w,m);Need(Render(w,s,"mask-rotated",10,50)>250,"Rotated stripe absent");Need(Render(w,s,"mask-rotated-outside",50,10)<3,"Unrotated stripe retained");});
        Check("U02-mask-stroke-no-fill",()=>{var w=Window();var s=new ShapeElement{Kind="rect",W=100,H=100,StrokeW=0};Add(w,s);var m=new ShapeElement{Kind="rect",X=10,Y=10,W=80,H=80,NoFill=true,Stroke="#ffffff",StrokeW=10,IsMask=true,MaskMode="luminance",MaskTargetId=s.Id};Add(w,m);Need(Render(w,s,"mask-stroke-center")<3,"No-fill mask center visible");Need(Render(w,s,"mask-stroke-edge",10,50)>250,"Mask stroke absent");});
        Check("U01-all-shape-fields-locked",()=>{var w=Window();var sh=new ShapeElement{Kind="roundrect",W=40,H=30,IsLocked=true,IsMask=true,StrokeW=2,Radius=5,Opacity=.8,Fill="#123456",Stroke="#654321"};Add(w,sh);foreach(var pair in new[]{("TxtFill","#ffffff"),("TxtStroke","#abcdef"),("TxtSizeW","80"),("TxtSizeH","80"),("TxtSizeX","80"),("TxtSizeY","80")})((TextBox)w.FindName(pair.Item1)).Text=pair.Item2;foreach(var n in new[]{"SldStrokeW","SldRadius","SldOpacity"})((Slider)w.FindName(n)).Value=15;foreach(var n in new[]{"ChkNoFill","ChkMask","ChkSquare"}){var c=(CheckBox)w.FindName(n);c.IsChecked=true;c.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));}((ComboBox)w.FindName("CmbMaskMode")).SelectedIndex=3;Call(w,"ApplySizeFields");Need(sh.W==40&&sh.H==30&&sh.Fill=="#123456"&&sh.Stroke=="#654321"&&sh.StrokeW==2&&sh.Radius==5&&sh.Opacity==.8&&!sh.NoFill&&!sh.LockAspect&&sh.MaskMode=="alpha","Locked shape field changed");Need(!((StackPanel)w.FindName("PanelShape")).IsEnabled,"Locked panel enabled");});
        Check("U01-text-ink-image-properties-locked",()=>{var w=Window();var tr=new TextRegionElement{IsLocked=true,Text="fixed",Color="#123456",FontSize=14};Add(w,tr);((TextBox)w.FindName("TxtRegionText")).Text="changed";((TextBox)w.FindName("TxtTextColor")).Text="#ffffff";((Slider)w.FindName("SldTextSize")).Value=20;Call(w,"SetAlign","right");Need(tr.Text=="fixed"&&tr.Color=="#123456"&&tr.FontSize==14&&tr.AlignH!="right","Locked text mutated");var ink=new InkElement{IsLocked=true,Color="#123456",Width=3,Opacity=.8,Points=new List<Point>{new(0,0),new(10,10)}};Add(w,ink);((TextBox)w.FindName("TxtInk")).Text="#ffffff";((Slider)w.FindName("SldInkW")).Value=10;((Slider)w.FindName("SldInkOpacity")).Value=20;Need(ink.Color=="#123456"&&ink.Width==3&&ink.Opacity==.8,"Locked ink mutated");var im=new ImageElement{IsLocked=true,Opacity=.8};Add(w,im);((Slider)w.FindName("SldImgOpacity")).Value=20;Need(im.Opacity==.8,"Locked image mutated");});
        Check("U02-target-transform-canvas",()=>{var w=Window();var box=new ShapeElement{Kind="rect",X=40,Y=0,W=20,H=100,Rotation=90,StrokeW=0,Fill="#ff0000"};Add(w,box);var m=new ShapeElement{Kind="rect",X=0,Y=0,W=100,H=100,StrokeW=0,Fill="#ffffff",IsMask=true,MaskTargetId=box.Id};Add(w,m);Call(w,"ApplyMasks");Field<Dictionary<CanvasElement,UIElement>>(w,"_views")[m].Visibility=Visibility.Hidden;var stage=(Canvas)w.FindName("Stage");stage.Measure(new Size(1200,700));stage.Arrange(new Rect(0,0,1200,700));stage.UpdateLayout();var bmp=new RenderTargetBitmap(100,100,96,96,PixelFormats.Pbgra32);bmp.Render(stage);var data=new byte[40000];bmp.CopyPixels(data,400,0);Need(data[(50*100+10)*4+3]>250,"Target rotation missing");Need(data[(10*100+50)*4+3]<3,"Unrotated target retained");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(Path.Combine(output,"wpf-target-rotation.png"));png.Save(file);});
        Check("U04-real-large-css-save-open",()=>{var w=Window();var els=Field<List<CanvasElement>>(w,"_elements");for(int i=0;i<990;i++)els.Add(new ShapeElement{Kind="rect",X=550,Y=300,W=10,H=10});var file=Path.Combine(output,"large-css-roundtrip.knotjot-ui");w.GetType().GetField("_currentPath",Flags).SetValue(w,file);Need((bool)Call(w,"Save",false),"Valid save rejected");using(var doc=ProjectFileService.Open(file))Need(doc.RootElement.GetProperty("css").GetString().Length>100000,"Fixture CSS too small");Need((bool)Call(w,"TryOpenPath",file),"Saved project not reopened");Need(Field<List<CanvasElement>>(w,"_elements").Count==991,"Roundtrip element count");Need(Field<List<string>>(w,"_undoStack").Count==0,"Open retained history");});
        Check("U01-component-properties-locked",()=>{var w=Window();var ce=ComponentLib.Find("card").CreateInstance();ce.IsLocked=true;ce.Props[0].Value="#123456";Add(w,ce);var panel=(StackPanel)w.FindName("PanelCompProps");var tb=panel.Children.OfType<StackPanel>().First().Children.OfType<TextBox>().First();tb.Text="#ffffff";Need(ce.Props[0].Value=="#123456","Locked component color mutated");panel.Children.OfType<Button>().Last().RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Need(ce.Props[0].Value=="#123456","Locked reset mutated color");});
        Check("U02-rotated-shape-interaction",()=>RotationInteractions.Shape(output));
        Check("U02-rotated-line-interaction",()=>RotationInteractions.Line(output));
        Check("U02-angled-box-image-ink-interaction",RotationInteractions.AngledBoxes);
        Check("U02-rotated-group-interaction",RotationInteractions.Group);
        Check("U02-exported-svg-pixels",()=>SvgSmoke.Run(output));
        Console.WriteLine("APPROVED_SMOKE failures="+failures);return failures==0?0:1;
    }
}




