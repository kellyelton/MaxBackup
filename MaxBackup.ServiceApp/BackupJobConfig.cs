using System.ComponentModel.DataAnnotations;

namespace MaxBackup.ServiceApp
{
    public class BackupJobConfig
    {
        [Required]
        public string Name { get; set; } = "<!!NOT SET";

        [Required]
        public string Source { get; set; } = "<!!NOT SET";

        [Required]
        public string Destination { get; set; } = "<!!NOT SET";

        [Required]
        public string[] Include { get; set; } = Array.Empty<string>();

        [Required]
        public string[] Exclude { get; set; } = Array.Empty<string>();

        public IReadOnlyDictionary<string, object> GetScope() {
            return new Dictionary<string, object> {
                ["Name"] = Name,
                ["Source"] = Source,
                ["Destination"] = Destination,
                ["Include"] = Include,
                ["Exclude"] = Exclude,
                ["name"] = "asdf"
            };
        }
    }
}