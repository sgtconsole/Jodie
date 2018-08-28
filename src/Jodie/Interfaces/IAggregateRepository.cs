namespace Jodie
{
    public interface IAggregateRepository<out TApplication> where TApplication : Aggregate
    {
        TApplication Get(string id);
    }
}