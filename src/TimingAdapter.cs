using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Qa3moz {
    internal static class TimingAdapter {
        // Source property names are selected explicitly after the supplied manifest is inspected.
        // No frame timings are guessed and no input file is ever modified.
        public static void Prepare(string sourcePath,string outputPath,string statesProperty,string durationProperty) {
            string source=Path.GetFullPath(sourcePath),output=Path.GetFullPath(outputPath);
            if(String.Equals(source,output,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The timing adapter must be a separate file from the original manifest");
            var json=new JavaScriptSerializer();
            var original=json.DeserializeObject(File.ReadAllText(source)) as Dictionary<string,object>;
            object rawStates;
            if(original==null || !original.TryGetValue(statesProperty,out rawStates))
                throw new InvalidDataException("Missing explicitly selected state dictionary: "+statesProperty);
            var states=rawStates as Dictionary<string,object>;
            if(states==null)throw new InvalidDataException("Selected state property must be a dictionary; inspect schema before adapting");
            var result=new Dictionary<string,object>();
            for(int i=0;i<PixelArt.Names.Length;i++) {
                string name=PixelArt.Names[i];object entry,rawTiming;
                if(!states.TryGetValue(name,out entry))throw new InvalidDataException("Missing state: "+name);
                var spec=entry as Dictionary<string,object>;
                if(spec==null || !spec.TryGetValue(durationProperty,out rawTiming))
                    throw new InvalidDataException("Missing explicitly selected timings: "+name+"."+durationProperty);
                result.Add(name,new Dictionary<string,object>{{"durations_ms",Read(rawTiming,PixelArt.Counts[i],name)}});
            }
            string text=json.Serialize(new Dictionary<string,object>{{"states",result}});
            // CreateNew also protects unrelated existing outputs and accidental repeat imports.
            using(var file=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(file,new UTF8Encoding(false)))writer.Write(text);
        }
        public static int[] Read(object raw,int expected,string name) {
            var values=raw as object[];
            if(values==null || values.Length!=expected)throw new InvalidDataException("Wrong timing count: "+name);
            int[] timing=new int[expected];
            for(int i=0;i<expected;i++) {
                if(!(values[i] is int) && !(values[i] is long))
                    throw new InvalidDataException("Timing must be an integer number of milliseconds: "+name);
                long value=Convert.ToInt64(values[i]);
                if(value<40 || value>5000)throw new InvalidDataException("Unbounded frame timing: "+name);
                timing[i]=(int)value;
            }
            return timing;
        }
    }
}
