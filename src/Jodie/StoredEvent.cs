namespace Jodie
{
    internal sealed class StoredEvent
    {
        public string TypeName { get; set; }
        public string Event { get; set; }
        public string AggregateId { get; set; }
        public int Sequence { get; set; }
    }
}