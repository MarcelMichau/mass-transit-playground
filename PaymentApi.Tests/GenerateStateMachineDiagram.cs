using MassTransit.SagaStateMachine;
using MassTransit.Visualizer;
using PaymentApi.StateMachine;

namespace PaymentApi.Tests;

public class GenerateStateMachineDiagram
{
    [Fact]
    public void ShouldGenerateStateMachineDiagram()
    {
        var stateMachine = new PaymentStateMachine();

        var graph = stateMachine.GetGraph();

        var generator = new StateMachineGraphvizGenerator(graph);

        var dots = generator.CreateDotFile();

        File.WriteAllText("payment-state-machine.dot", dots);
    }
}