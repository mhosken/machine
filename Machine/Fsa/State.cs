﻿using System;
using System.Collections.Generic;
using System.Linq;
using SIL.Collections;
using SIL.Machine.FeatureModel;

namespace SIL.Machine.Fsa
{
	public class State<TData, TOffset> : IEquatable<State<TData, TOffset>>  where TData : IData<TOffset>
	{
		private readonly int _index;
		private readonly List<Arc<TData, TOffset>> _arcs;

		private readonly bool _isAccepting;
		private readonly List<AcceptInfo<TData, TOffset>> _acceptInfos; 
		private readonly List<TagMapCommand> _finishers;
		private readonly bool _isLazy;
		private readonly IComparer<Arc<TData, TOffset>> _arcComparer; 

		internal State(int index, bool isAccepting)
			: this(index, isAccepting, Enumerable.Empty<AcceptInfo<TData, TOffset>>(), Enumerable.Empty<TagMapCommand>(), false)
		{
		}

		internal State(int index, IEnumerable<AcceptInfo<TData, TOffset>> acceptInfos)
			: this(index, true, acceptInfos, Enumerable.Empty<TagMapCommand>(), false)
		{
		}

		internal State(int index, IEnumerable<AcceptInfo<TData, TOffset>> acceptInfos, IEnumerable<TagMapCommand> finishers, bool isLazy)
			: this(index, true, acceptInfos, finishers, isLazy)
		{
		}

		private State(int index, bool isAccepting, IEnumerable<AcceptInfo<TData, TOffset>> acceptInfos, IEnumerable<TagMapCommand> finishers, bool isLazy)
		{
			_index = index;
			_isAccepting = isAccepting;
			_acceptInfos = new List<AcceptInfo<TData, TOffset>>(acceptInfos);
			_finishers = new List<TagMapCommand>(finishers);
			_isLazy = isLazy;
			_arcs = new List<Arc<TData, TOffset>>();
			_arcComparer = ProjectionComparer<Arc<TData, TOffset>>.Create(arc => arc.PriorityType).Reverse();
		}

		public int Index
		{
			get
			{
				return _index;
			}
		}

		public bool IsAccepting
		{
			get
			{
				return _isAccepting;
			}
		}

		public IEnumerable<Arc<TData, TOffset>> Arcs
		{
			get { return _arcs; }
		}

		public IEnumerable<AcceptInfo<TData, TOffset>> AcceptInfos
		{
			get { return _acceptInfos; }
		}

		public bool IsLazy
		{
			get { return _isLazy; }
		}

		internal List<TagMapCommand> Finishers
		{
			get
			{
				return _finishers;
			}
		}

		public State<TData, TOffset> AddArc(State<TData, TOffset> target)
		{
			return AddArc(target, ArcPriorityType.Medium);
		}

		public State<TData, TOffset> AddArc(State<TData, TOffset> target, ArcPriorityType priorityType)
		{
			return AddArc(new Arc<TData, TOffset>(this, target, priorityType));
		}

		public State<TData, TOffset> AddArc(FeatureStruct condition, State<TData, TOffset> target)
		{
			return AddArc(new Arc<TData, TOffset>(this, condition, target));
		}

		internal State<TData, TOffset> AddArc(State<TData, TOffset> target, int tag)
		{
			return AddArc(new Arc<TData, TOffset>(this, target, tag));
		}

		internal State<TData, TOffset> AddArc(FeatureStruct condition, State<TData, TOffset> target, IEnumerable<TagMapCommand> cmds)
		{
			return AddArc(new Arc<TData, TOffset>(this, condition, target, cmds));
		}

		private State<TData, TOffset> AddArc(Arc<TData, TOffset> arc)
		{
			int index = _arcs.BinarySearch(arc, _arcComparer);
			if (index < 0)
				index = ~index;
			_arcs.Insert(index, arc);
			return arc.Target;
		}

		public override int GetHashCode()
		{
			return _index;
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			return Equals(obj as State<TData, TOffset>);
		}

		public bool Equals(State<TData, TOffset> other)
		{
			if (other == null)
				return false;

			return _index == other._index;
		}

		public override string ToString()
		{
			return string.Format("State {0}", _index);
		}
	}
}
