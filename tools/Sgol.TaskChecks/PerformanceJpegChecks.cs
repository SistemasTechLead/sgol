using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using SkiaSharp;
using Sgol.Evidence.Technical;
using Sgol.Evidence.Contracts;

internal static class PerformanceJpegChecks
{
    internal static async Task GoldenAsync()
    {
        var image=PerformanceMediaChecks.Photograph();var validator=new FileTechnicalValidator();
        var sos=Enumerable.Range(0,image.Length-1).First(i=>image[i]==255&&image[i+1]==218);
        var start=sos+2+(image[sos+2]<<8)+image[sos+3];
        var cases=new List<(byte[] Bytes,bool Valid)> { (image,true),(image[..^1],false),(image.Concat(new byte[]{1}).ToArray(),false),(new byte[]{255,216,255,217},false) };
        foreach(var marker in new[]{new byte[]{255,0},new byte[]{255,255,0},new byte[]{255,208},new byte[]{255,217}}){
            var suffix=marker[^1]==217?Array.Empty<byte>():new byte[]{0,0,255,217};
            cases.Add((image[..start].Concat(new byte[EvidenceFileLimits.BufferBytes-1]).Concat(marker).Concat(suffix).ToArray(),true));
        }
        foreach(var (bytes,valid) in cases){using var input=new MemoryStream(bytes,false);var result=await validator.ValidateAsync(input,"image/jpeg","synthetic.jpg",default);if(result.File is {} file)await file.DisposeAsync();if(result.IsValid!=valid)throw new Exception("JPEG marker boundary regression");}
        Console.WriteLine("PASS JPEG golden cases: valid, truncated, trailing bytes, missing frame, stuffing/fill/restart/end across block boundaries");
    }
    public static async Task RunAsync()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory); while(!File.Exists(Path.Combine(root.FullName,"SistemaSGOL.slnx")))root=root.Parent!;
        var path = Path.Combine(root.FullName,".local","performance-baseline-source","src","Modules","Evidence","bin","Release","net10.0","Sgol.Evidence.dll");
        if(!File.Exists(path))throw new Exception("Build the preserved pre-change source before differential validation");
        var load = new AssemblyLoadContext("Pre-change JPEG oracle",true);
        try {
            var type=load.LoadFromAssemblyPath(path).GetType("Sgol.Evidence.Technical.FileTechnicalValidator")!;
            var instance=Activator.CreateInstance(type)!;var method=type.GetMethod("ValidateAsync")!;
            var current=new FileTechnicalValidator();
            async Task<(bool Valid,double Ms)> Validate(byte[] bytes,bool legacy) {
                using var stream=new MemoryStream(bytes,false);var watch=Stopwatch.StartNew();
                if(legacy){var task=(Task)method.Invoke(instance,new object[]{stream,"image/jpeg","synthetic.jpg",CancellationToken.None})!;await task;var result=task.GetType().GetProperty("Result")!.GetValue(task)!;var valid=(bool)result.GetType().GetProperty("IsValid")!.GetValue(result)!;if(result.GetType().GetProperty("File")!.GetValue(result) is IAsyncDisposable file)await file.DisposeAsync();return(valid,watch.Elapsed.TotalMilliseconds);}
                var answer=await current.ValidateAsync(stream,"image/jpeg","synthetic.jpg",default);if(answer.File is {} content)await content.DisposeAsync();return(answer.IsValid,watch.Elapsed.TotalMilliseconds);
            }
            byte[] Image(int width,int height,bool noise){using var bitmap=new SKBitmap(width,height);if(noise){var pixels=new SKColor[width*height];var random=new Random(42);for(var i=0;i<pixels.Length;i++)pixels[i]=new((byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256));bitmap.Pixels=pixels;}else{using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.Blue);}using var encoded=bitmap.Encode(SKEncodedImageFormat.Jpeg,85);return encoded.ToArray();}
            var small=Image(64,32,false);var large=Image(1600,1200,true);
            var cases=new List<(string,byte[])>{("small",small),("same-http-noise-1600x1200",large),("missing-end-byte",small[..^1]),("missing-end-marker",small[..^2]),("trailing-byte",small.Concat(new byte[]{0}).ToArray()),("no-frame",new byte[]{255,216,255,217}),("wrong-mime-content",new byte[]{60,115,99,114,105,112,116,62})};
            var sos=Enumerable.Range(0,small.Length-1).First(i=>small[i]==255&&small[i+1]==218);var entropy=sos+2+(small[sos+2]<<8)+small[sos+3];
            foreach(var marker in new[]{new byte[]{255,0},new byte[]{255,255,0},new byte[]{255,208},new byte[]{255,217}}){var body=new byte[EvidenceFileLimits.BufferBytes-1];var suffix=marker[^1]==217?Array.Empty<byte>():new byte[]{0,0,0,255,217};cases.Add(("cross-read-marker-"+Convert.ToHexString(marker),small[..entropy].Concat(body).Concat(marker).Concat(suffix).ToArray()));}
            var samples=new List<object>();foreach(var (name,bytes) in cases){var before=await Validate(bytes,true);var after=await Validate(bytes,false);if(before.Valid!=after.Valid)throw new Exception("Structural validation changed: "+name);samples.Add(new{name,inputBytes=bytes.Length,beforeMs=before.Ms,afterMs=after.Ms,valid=after.Valid,same=true});Console.WriteLine($"PASS JPEG {name}: same={after.Valid}; {before.Ms:F1} -> {after.Ms:F1}ms");}
            var output=Path.Combine(root.FullName,"artifacts","performance-jpeg");Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"differential.json"),JsonSerializer.Serialize(samples,new JsonSerializerOptions{WriteIndented=true}));
        } finally { load.Unload(); }
    }
}
