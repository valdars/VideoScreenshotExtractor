using CommandLine;

namespace VideoScreenshotExtractor
{
    internal class Options
    {
        [Value(0, Required = true, HelpText = "Path to source folder that contains video files")]
        public string Path { get; init; } = default!;

        [Option(Default = 5, HelpText = "Interval between screenshots in seconds")]
        public int Interval { get; set; }

        [Option(Default = "Thumbnails", HelpText = "Output directory for screenshots")]
        public string OutputDirectory { get; set; } = default!;

        [Option(Default = 4, HelpText = "Number of parallel extractions to run")]
        public int Threads { get; set; } = default!;
    }
}
