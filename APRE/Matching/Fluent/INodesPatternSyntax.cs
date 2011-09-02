using System;

namespace SIL.APRE.Matching.Fluent
{
	public interface INodesPatternSyntax<TOffset> : IInitialNodesPatternSyntax<TOffset>
	{
		INodesPatternSyntax<TOffset> Expression(Func<IExpressionSyntax<TOffset>, IExpressionSyntax<TOffset>> build);
		INodesPatternSyntax<TOffset> Expression(string name, Func<IExpressionSyntax<TOffset>, IExpressionSyntax<TOffset>> build);

		Pattern<TOffset> Value { get; }
	}
}
