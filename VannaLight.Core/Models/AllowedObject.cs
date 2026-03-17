namespace VannaLight.Core.Models
{
    public sealed class AllowedObject
    {
        public long Id { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string SchemaName { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public string ObjectType { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
    }
}