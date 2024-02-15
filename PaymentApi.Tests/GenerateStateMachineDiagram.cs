using MassTransit.SagaStateMachine;
using MassTransit.Visualizer;
using PaymentApi.StateMachine;
using QuikGraph;

namespace PaymentApi.Tests;

public class GenerateStateMachineDiagram
{
    [Fact]
    public void ShouldGenerateStateMachineDiagram()
    {
        var stateMachine = new PaymentStateMachine();

        var graph = stateMachine.GetGraph();

        var generator = new StateMachineGraphvizGenerator(graph);

        string dots = generator.CreateDotFile();
    }
}