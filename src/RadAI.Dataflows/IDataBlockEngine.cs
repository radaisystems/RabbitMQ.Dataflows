using System.Threading.Tasks;

namespace RadAI.Dataflows;

public interface IDataBlockEngine<TIn>
{
    ValueTask EnqueueWorkAsync(TIn data);
}