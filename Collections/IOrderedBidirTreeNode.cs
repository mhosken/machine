namespace SIL.Collections
{
	public interface IOrderedBidirTreeNode<TNode> : IBidirTreeNode<TNode>, IOrderedBidirListNode<TNode> where TNode : class, IOrderedBidirTreeNode<TNode>
	{
		new IOrderedBidirList<TNode> Children { get; }
	}
}
