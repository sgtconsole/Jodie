namespace Jodie
{
    public interface IRoute<in TEvent>
    {
        void Handle(TEvent e);
    }
}