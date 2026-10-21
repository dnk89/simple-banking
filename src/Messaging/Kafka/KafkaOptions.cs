using System.ComponentModel.DataAnnotations;

namespace Messaging.Kafka;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    [Required(AllowEmptyStrings = false)]
    public string BootstrapServers { get; set; } = "";

    public string GroupIdSuffix { get; set; } = "";
}
