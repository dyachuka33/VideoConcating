using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static VideoOptimizer.Program.VideoClipperWrapper;

namespace VideoOptimizer
{
    
    internal class Program
    {
        const int BITRATE_PER_AUDIO_CHANNEL = 128;
        public class VideoClipperWrapper {
            static int ffmpeg_process_id; //process id of started ffmpeg process - used to close it if user cancels or pauses
            public class audio_info
            {
                #region Properties
                public string codec_long_name { get; set; }
                public double bit_rate { get; set; }
                public string duration { get; set; }
                public string language { get; set; }
                public string channel_layout { get; set; }
                #endregion
            }
            public class subtitle_info
            {
                #region Properties
                public string codec_long_name { get; set; }
                public string language { get; set; }
                #endregion
            }
            public class video_info
            {
                #region Properties
                public double duration { get; set; } = 0;
                public string codec_long_name { get; set; }
                public double bit_rate { get; set; }
                public string profile { get; set; }
                public int width { get; set; } = 0;
                public int height { get; set; } = 0;
                public int crop_width { get; set; } = 0;
                public int crop_height { get; set; } = 0;
                public int crop_x { get; set; } = 0;
                public int crop_y { get; set; } = 0;
                public string crop_params { get; set; } = "";
                public string r_frame_rate { get; set; }
                #endregion
            }
            public class file_info
            {
                #region Properties
                public string filename { get; set; }
                public double size { get; set; }
                public int audio_channel_nums { get; set; }
                #endregion
            }

            string json = "";
            private int ffprobe(string input)
            {
                try
                {
                    System.Diagnostics.ProcessStartInfo procffprobe;

                    procffprobe = new System.Diagnostics.ProcessStartInfo("cmd", "/c " + " ffprobe -v quiet -print_format json -show_format -show_streams \"" + input + "\"");// Windows: define Process Info to assing to the process
                                                                                                                                                                              // The following commands are needed to redirect the standard output and standard error.
                                                                                                                                                                              // This means that it will be redirected to the Process.StandardOutput StreamReader.
                    procffprobe.RedirectStandardOutput = true;
                    procffprobe.RedirectStandardInput = true;
                    procffprobe.RedirectStandardError = true;
                    procffprobe.UseShellExecute = false;
                    procffprobe.CreateNoWindow = true;  // Do not create the black window.
                    procffprobe.WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);//set path of vtc.exe same as ffmpeg.exe

                    Process ffprobeproc = new Process();
                    ffprobeproc.StartInfo = procffprobe;
                    ffprobeproc.OutputDataReceived += (sender, args) => ffprobeOutput(args.Data);
                    ffprobeproc.ErrorDataReceived += (sender, args) => ffprobeOutput(args.Data); //same method used for error data
                    ffprobeproc.Start();                //start the ffprobe
                    ffprobeproc.BeginOutputReadLine();  // Set our event handler to asynchronously read the sort output.
                    ffprobeproc.BeginErrorReadLine();
                    ffprobeproc.WaitForExit();          //since it is started as separate thread, GUI will continue separately, but we wait here before starting next task
                    ffprobeproc.CancelOutputRead(); //stop reading redirected standard output
                    ffprobeproc.CancelErrorRead();

                    //Thread.Sleep(500);
                    return 0;                   //0 means OK, not used so far
                }
                catch
                {
                    return -1;
                }
            }

            private void ffprobeOutput(string output)
            {       //read output sent from ffprobe
                try
                {
                    json += output; //put it in string to be parsed to get file info
                }
                catch { }

            }
            private void ff_RunWorkerCompleted()
            {   //it sorts out file info data after ffprobe parses it and fills in variables to pass to infoForm
                try
                {
                    int count_aud_streams, count_sub_streams;
                    dynamic JSON_helper;
                    bool video_exists;
                    count_aud_streams = Regex.Matches(json, "\"audio\"").Count;
                    count_sub_streams = Regex.Matches(json, "\"subtitle\"").Count;
                    Dictionary<int, string> comboSource = new Dictionary<int, string>(); //create new collection for combo

                    File_info = new file_info { };
                    File_info.audio_channel_nums = count_aud_streams;
                    Video_info = new video_info { };
                    Audio_info = new audio_info[count_aud_streams];
                    Subtitle_info = new subtitle_info[count_sub_streams];
                    double duration = 0.0;
                    video_exists = (Regex.Matches(json, "\"video\"").Count > 0);
                    JSON_helper = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                    string j_duration = "";
                    try
                    {
                        j_duration = JSON_helper.format.duration;
                    }
                    catch (RuntimeBinderException)
                    { }

                    File_info.filename = JSON_helper.format.filename;
                    File_info.size = JSON_helper.format.size / 1048576;

                    if (video_exists)
                    {
                        try
                        {
                            Video_info.codec_long_name = JSON_helper.streams[0].codec_long_name;
                        }
                        catch (RuntimeBinderException)
                        { Video_info.codec_long_name = ""; }
                        try
                        {
                            Video_info.profile = JSON_helper.streams[0].profile;
                        }
                        catch (RuntimeBinderException) { Video_info.profile = ""; }
                        try
                        {
                            Video_info.width = JSON_helper.streams[0].coded_width;
                        }
                        catch (RuntimeBinderException) { Video_info.width = 0; }
                        try
                        {
                            Video_info.height = JSON_helper.streams[0].coded_height;
                        }
                        catch (RuntimeBinderException) { Video_info.height = 0; }
                        try
                        {
                            Video_info.r_frame_rate = JSON_helper.streams[0].r_frame_rate;
                        }
                        catch (RuntimeBinderException) { Video_info.r_frame_rate = ""; }

                        double v_duration = 0.0;
                        try
                        {
                            v_duration = JSON_helper.streams[0].duration;
                        }
                        catch (RuntimeBinderException)
                        { v_duration = 0.0; }
                        if (v_duration != 0.0)
                        {
                            Video_info.duration = v_duration;
                            duration = Video_info.duration;
                        }
                        else
                            Video_info.duration = duration;
                        TimeSpan dur = TimeSpan.FromSeconds(Video_info.duration);
                        string j_bitrate = "";
                        try
                        {
                            j_bitrate = JSON_helper.streams[0].bit_rate;
                        }
                        catch (RuntimeBinderException)
                        { }
                        if (j_bitrate != "" && j_duration != null)
                            Video_info.bit_rate = Double.Parse(j_bitrate, CultureInfo.GetCultureInfo("en-US"));
                        else
                            Video_info.bit_rate = 0.0;

                    }
                    else //no video, just audio
                    {
                        for (int i = 0; i <= count_aud_streams - 1; i++)
                        {
                            Audio_info[i] = new audio_info();   //Initialize new object
                            try
                            {
                                Audio_info[i].codec_long_name = JSON_helper.streams[i].codec_long_name;
                            }
                            catch (RuntimeBinderException)
                            { Audio_info[i].codec_long_name = ""; }
                            try
                            {
                                Audio_info[i].channel_layout = JSON_helper.streams[i].channel_layout;
                            }
                            catch (RuntimeBinderException)
                            { Audio_info[i].channel_layout = ""; }
                            string j_audioDuration = "";
                            try
                            {
                                j_audioDuration = JSON_helper.streams[i].duration;
                            }
                            catch (RuntimeBinderException)
                            { }
                            if (j_audioDuration != "" && j_audioDuration != null)
                            {
                                Audio_info[i].duration = j_audioDuration;
                                Audio_info[i].duration = Audio_info[i].duration.Substring(0, Audio_info[i].duration.IndexOf('.'));
                                double sec = Double.Parse(Audio_info[i].duration, CultureInfo.GetCultureInfo("en-US"));
                                TimeSpan ts = TimeSpan.FromSeconds(sec);
                                Audio_info[i].duration = String.Format("{0:g}", ts);
                            }
                            else
                                Audio_info[i].duration = duration.ToString();
                            try
                            {
                                Audio_info[i].bit_rate = Double.Parse(JSON_helper.streams[i].bit_rate, CultureInfo.GetCultureInfo("en-US"));
                            }
                            catch (RuntimeBinderException)
                            { Audio_info[i].bit_rate = 0.0; }

                        }

                        if (count_aud_streams > 0 && video_exists)
                        {
                            for (int i = 1; i <= count_aud_streams; i++)
                            {
                                Audio_info[i - 1] = new audio_info();   //Initialize new object
                                try
                                {
                                    Audio_info[i - 1].codec_long_name = JSON_helper.streams[i].codec_long_name;
                                }
                                catch (RuntimeBinderException)
                                { Audio_info[i - 1].codec_long_name = ""; }
                                try
                                {
                                    Audio_info[i - 1].channel_layout = JSON_helper.streams[i].channel_layout;
                                }
                                catch (RuntimeBinderException)
                                { Audio_info[i - 1].channel_layout = ""; }
                                try
                                {
                                    j_duration = JSON_helper.streams[i].duration;
                                }
                                catch (RuntimeBinderException)
                                { j_duration = ""; }
                                if (j_duration != "" && j_duration != null)
                                {
                                    Audio_info[i - 1].duration = j_duration;
                                    Audio_info[i - 1].duration = Audio_info[i - 1].duration.Substring(0, Audio_info[i - 1].duration.IndexOf('.'));
                                    double sec = Double.Parse(Audio_info[i - 1].duration, CultureInfo.GetCultureInfo("en-US"));
                                    TimeSpan ts = TimeSpan.FromSeconds(sec);
                                    Audio_info[i - 1].duration = String.Format("{0:g}", ts);
                                }
                                else
                                    Audio_info[i - 1].duration = duration.ToString();
                                string j_bitrate = "";
                                try
                                {
                                    j_bitrate = JSON_helper.streams[i].bit_rate;
                                }
                                catch (RuntimeBinderException)
                                { }
                                if (j_bitrate != "" && j_bitrate != null)
                                    Audio_info[i - 1].bit_rate = Double.Parse(j_bitrate, CultureInfo.GetCultureInfo("en-US"));
                                try
                                {
                                    Audio_info[i - 1].language = JSON_helper.streams[i].tags.language;
                                }
                                catch (RuntimeBinderException)
                                { Audio_info[i - 1].language = ""; }
                            }
                        }
                        if (count_sub_streams > 0)
                        {
                            for (int i = 1; i <= count_sub_streams; i++)
                            {
                                Subtitle_info[i - 1] = new subtitle_info(); //Initialize new object
                                try
                                {
                                    Subtitle_info[i - 1].codec_long_name = JSON_helper.streams[i + count_aud_streams].codec_name;
                                }
                                catch (RuntimeBinderException)
                                { Subtitle_info[i - 1].codec_long_name = ""; }
                                try
                                {
                                    Subtitle_info[i - 1].language = JSON_helper.streams[i + count_aud_streams].tags.language;
                                }
                                catch (RuntimeBinderException)
                                { Subtitle_info[i - 1].language = ""; }
                            }
                        }
                    }
                }
                catch (Exception x)
                {
                    string msg = x.Message;
                }
            }

            void Ffmpeg_OutputDataReceived(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null && e.Data.StartsWith("frame="))
                {
                    //todo: show progress
                    Console.WriteLine(e.Data);
                }
            }
            void Ffmpeg_ErrorDataReceived(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    //todo: show progress
                    Console.WriteLine(e.Data);
                }
            }

            void Ffmpeg_CropOutputDataReceived(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null && e.Data.Contains("crop="))
                {
                    Regex regex = new Regex(@"crop=(\d+:\d+:\d+:\d+)");
                    Match match = regex.Match(e.Data);
                    if (match.Success)
                    {
                        string cropValues = match.Groups[1].Value;
                        Video_info.crop_params = "crop=" + cropValues;
                    }
                    else
                    {

                    }
                }
            }
            void Ffmpeg_CropErrorDataReceived(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null && e.Data.Contains("crop="))
                {
                    Regex regex = new Regex(@"crop=(\d+:\d+:\d+:\d+)");
                    Match match = regex.Match(e.Data);
                    if (match.Success)
                    {
                        string cropValues = match.Groups[1].Value;
                        Video_info.crop_params = "crop=" + cropValues;
                    }
                    else
                    {

                    }
                }
            }
            public void FileProperties(string input_file)
            {
                try
                {
                    json = "";
                    ffprobe(input_file);
                    ff_RunWorkerCompleted();
                }
                catch { }

            }

            public int CropDetect(string input_file)
            {
                try
                {
                    Process proc = new System.Diagnostics.Process(); //process that call cmd.exe to execute ffmpeg task
                    System.Diagnostics.ProcessStartInfo procStartffmpeg;
                    string command = $"ffmpeg -i \"{input_file}\" -t 1 -vf cropdetect -f mp4 NUL";
                    procStartffmpeg = new System.Diagnostics.ProcessStartInfo("cmd", "/c  " + command);
                                                                                                       
                    procStartffmpeg.RedirectStandardOutput = true;
                    procStartffmpeg.RedirectStandardInput = true;
                    procStartffmpeg.RedirectStandardError = true;
                    procStartffmpeg.UseShellExecute = false;
                    procStartffmpeg.CreateNoWindow = true;  // Do not create the black window.
                    procStartffmpeg.WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);//set path of vtc.exe same as ffmpeg.exe

                    proc.StartInfo = procStartffmpeg;   // Now we assign process its ProcessStartInfo and start it
                    proc.Start();               //start the ffmpeg
                    proc.OutputDataReceived += Ffmpeg_CropOutputDataReceived;
                    proc.ErrorDataReceived += Ffmpeg_CropErrorDataReceived;
                    ffmpeg_process_id = proc.Id;//remember process id so that it can be closed if user cancels
                    proc.BeginOutputReadLine(); // Set our event handler to asynchronously read the sort output.
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();         //since it is started as separate thread, GUI will continue separately, but we wait here before starting next task
                    proc.CancelOutputRead();    //stop reading redirected standard output
                    proc.CancelErrorRead();
                    return 0;                   //0 means OK, not used so far
                }
                catch (Exception ex)
                {
                    return -1;                  //-1 means NOT OK, not used so far
                }
            }

            public int batchTask(string current_task)
            {           //called when starting each ffmpeg encoding task, passed task string as parameter
                try
                {
                    Process proc = new System.Diagnostics.Process(); //process that call cmd.exe to execute ffmpeg task
                    System.Diagnostics.ProcessStartInfo procStartffmpeg;

                    procStartffmpeg = new System.Diagnostics.ProcessStartInfo("cmd", "/c  " + current_task);// Windows: define Process Info to assing to the process
                                                                                                            // The following commands are needed to redirect the standard output and standard error.
                                                                                                            // This means that it will be redirected to the Process.StandardOutput StreamReader.
                    procStartffmpeg.RedirectStandardOutput = true;
                    procStartffmpeg.RedirectStandardInput = true;
                    procStartffmpeg.RedirectStandardError = true;
                    procStartffmpeg.UseShellExecute = false;
                    procStartffmpeg.CreateNoWindow = true;  // Do not create the black window.
                    procStartffmpeg.WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);//set path of vtc.exe same as ffmpeg.exe

                    proc.StartInfo = procStartffmpeg;   // Now we assign process its ProcessStartInfo and start it
                    proc.Start();               //start the ffmpeg
                    proc.OutputDataReceived += Ffmpeg_OutputDataReceived;
                    proc.ErrorDataReceived += Ffmpeg_ErrorDataReceived;
                    ffmpeg_process_id = proc.Id;//remember process id so that it can be closed if user cancels
                    Thread.Sleep(500);          //wait a little bit - prevent glitches for concurrent threads
                    proc.BeginOutputReadLine(); // Set our event handler to asynchronously read the sort output.
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();         //since it is started as separate thread, GUI will continue separately, but we wait here before starting next task
                    proc.CancelOutputRead();    //stop reading redirected standard output
                    proc.CancelErrorRead();
                    return 0;                   //0 means OK, not used so far
                }
                catch (Exception ex)
                {
                    return -1;                  //-1 means NOT OK, not used so far
                }
            }
            public file_info File_info;
            public video_info Video_info = new video_info { };
            public audio_info[] Audio_info;
            public subtitle_info[] Subtitle_info;
        }
       

        static void Main(string[] args)
        {
            Stopwatch stopwatch = new Stopwatch(); //measure execution time for each job
            VideoClipperWrapper converter = new VideoClipperWrapper();
            string firstFilePath = "D:\\testVideo\\1.mp4";
            string secondFilePath = "D:\\testVideo\\2.MOV";
            string clip1Path = "D:\\output\\clip1.mp4";
            string clip2Path = "D:\\output\\clip2.mp4";
            string clip3Path = "D:\\output\\clip3.mp4";
            string concatedVideo1 = "D:\\output\\temp1.mp4";
            string concatedVideo2 = "D:\\output\\temp2.mp4";
            string outputFilePath = "D:\\output\\output.mp4";
            string clipCommand1 = $"ffmpeg -y -i \"{firstFilePath}\" -c:v libx264 -c:a aac -r 30 -vf \"scale=1920*1080,setsar=1:1\" -t 6 \"{clip1Path}\"";
            converter.batchTask(clipCommand1);
            string clipCommand2 = $"ffmpeg -y -i \"{firstFilePath}\" -c:v libx264 -c:a aac -r 30 -vf \"scale=1920*1080,setsar=1:1\" -ss 55 \"{clip2Path}\"";
            converter.batchTask(clipCommand2);
            string clipCommand3 = $"ffmpeg -y -i \"{secondFilePath}\" -c:v libx264 -c:a aac -r 30 -vf \"scale=1920*1080,setsar=1:1\"  \"{clip3Path}\"";
            converter.batchTask(clipCommand3);
            string concatCommand1 = $"ffmpeg -y -i \"{clip1Path}\" -i \"{clip3Path}\" -filter_complex [0:v][0:a][1:v][1:a]concat=n=2:v=1:a=1[outv][outa] -map \"[outv]\" -map \"[outa]\" \"{concatedVideo1}\"";
            converter.batchTask(concatCommand1);
            string concatCommand2 = $"ffmpeg -y -i \"{concatedVideo1}\" -i \"{clip2Path}\" -filter_complex [0:v][0:a][1:v][1:a]concat=n=2:v=1:a=1[outv][outa] -map \"[outv]\" -map \"[outa]\" \"{concatedVideo2}\"";
            converter.batchTask(concatCommand2);
            string demuxCommand = $"ffmpeg -y -i \"{concatedVideo2}\" -i \"{firstFilePath}\" -c:v copy -c:a copy -map 0:v:0 -map 1:a:0 -shortest \"{outputFilePath}\"";
            converter.batchTask(demuxCommand);
            //File.Delete(clip1Path);
            //File.Delete(clip2Path);
            //File.Delete(clip3Path);
            //File.Delete(concatedVideo);
        }
    }
}
