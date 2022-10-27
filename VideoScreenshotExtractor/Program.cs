using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommandLine;
using Instances;

namespace VideoScreenshotExtractor
{
    internal class Program
    {
        private static readonly string[] videoExtensions = new[] { "mkv", "avi", "mp4", "mov", "webm", "wmv" };
        private static readonly CancellationTokenSource cancellationTokenSource = new();
        private static string ffmpegPath = null!;
        private static string ffprobePath = null!;

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            ffmpegPath = Path.Combine(Environment.CurrentDirectory, "ffmpeg", "ffmpeg.exe");
            ffprobePath = Path.Combine(Environment.CurrentDirectory, "ffmpeg", "ffprobe.exe");
            var result = Parser.Default.ParseArguments<Options>(args);
            await result.WithParsedAsync(RunAsync);
            result.WithNotParsed(HandleParseError);
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            cancellationTokenSource.Cancel();
        }

        static void HandleParseError(IEnumerable<Error> errs)
        {
            if (errs.IsVersion() || errs.IsHelp())
            {
                return;
            }

            Console.WriteLine("Missing or invalid arguments");
        }

        static async Task RunAsync(Options opts)
        {
            var timer = new Stopwatch();
            timer.Start();

            var outputDirectory = PathHelper.RelativePathToAbsolute(opts.OutputDirectory, opts.Path);
            Directory.CreateDirectory(outputDirectory);

            var files = PathHelper.GetFilesOfExtensionsFromPath(opts.Path, videoExtensions);
            Console.WriteLine($"Starting extracting thumbnails from {files.Count} files");
            var parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = opts.Threads
            };
            await Parallel.ForEachAsync(files, parallelOptions, async (file, token) => await ExtractThumbnails(file, opts));
            timer.Stop();
            Console.WriteLine($"Finished in {timer.Elapsed}");
        }

        private static async Task ExtractThumbnails(string path, Options opts)
        {
            // want to skip cancellation for already finished process to avoid error
            var canceler = new CancellationTokenSource();
            cancellationTokenSource.Token.Register(() => canceler?.Cancel());

            Console.WriteLine($"Starting extracting from {path}");

            var duration = await GetDuration(path);
            var hasHours = duration.TotalHours >= 1;
            var count = (int)Math.Floor(duration.TotalSeconds / opts.Interval);
            // generate timestamps in seconds for all points of thumbnail extraction
            var timestamps = Enumerable.Range(1, count).Select(x => x * opts.Interval);
            // handle extracting in batches to reduce overhead
            var batches = timestamps.Batch(40);
            var thumbnailPath = Path.Combine(PathHelper.RelativePathToAbsolute(opts.OutputDirectory, opts.Path), Path.GetFileNameWithoutExtension(path));
            foreach (var batch in batches)
            {
                var args = new StringBuilder("-hwaccel auto -y ");
                foreach (var timestamp in batch)
                {
                    args.Append($"-ss {timestamp} -i \"{path}\" ");
                }

                var index = 0;
                foreach (var timestamp in batch)
                {
                    var timespan = TimeSpan.FromSeconds(timestamp);
                    var formatted = hasHours ? timespan.ToString(@"hh\-mm\-ss") : timespan.ToString(@"mm\-ss");
                    args.Append($"-map {index}:v -vframes 1 \"{thumbnailPath} {formatted}.jpg\" ");
                    index++;
                }

                var result = await Instance.FinishAsync(ffmpegPath, args.ToString(), canceler.Token);
                if (result.ExitCode != 0)
                {
                    throw new Exception($"Extracting failed with error: {result.ErrorData[^1]}");
                }
            }
            canceler.Dispose();
            canceler = null;
            Console.WriteLine($"Finished extracting for {path}");
        }

        private static async Task<TimeSpan> GetDuration(string path)
        {
            var result = await Instance.FinishAsync(ffprobePath, $"-loglevel error -print_format ini -show_format \"{path}\"");
            var raw = result.OutputData.SingleOrDefault(x => x.StartsWith("duration="));
            if (raw == null)
            {
                throw new Exception("Unable to find duration");
            }
            var durationStr = raw.Split('=')[1];
            var durationNumber = Convert.ToDouble(durationStr, CultureInfo.InvariantCulture);
            var duration = TimeSpan.FromSeconds(durationNumber);
            return duration;
        }
    }
}