using CommandLine;
using CommandLine.Text;

namespace CanonHandler
{
    public class Settings
    {

        [Option('p', "pipe", Required = true,
            HelpText = "Anonymous pipe handle.")]
        public string PipeHandle { get; set; }

        [Option('d', "device", DefaultValue = 0,
            HelpText = "Device index to use (camera index).")]
        public int DeviceIndex { get; set; }

        [Option('a', "av", DefaultValue = "5.6",
            HelpText = "Camera aperture value.")]
        public string Av { get; set; }

        [Option('t', "tv", DefaultValue = "1/50",
            HelpText = "Camera shutter speed.")]
        public string Tv { get; set; }

        [Option('i', "iso", DefaultValue = "ISO 800",
            HelpText = "Camera ISO speed value.")]
        public string ISO { get; set; }
        
        [Option('w', "wb", DefaultValue = 4,
            HelpText = "Camera white balance preset value.")]
        public int WB { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
                current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
        
    }
}
