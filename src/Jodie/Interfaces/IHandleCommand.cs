using System.Collections;

namespace Jodie
{
    public interface IHandleCommand<in TCommand>
    {
        IEnumerable Handle(TCommand c);
    }
}
