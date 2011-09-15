﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SIL.APRE.FeatureModel;

namespace SIL.APRE.Fsa
{
	public enum PriorityType
	{
		High = 0,
		Medium,
		Low
	}

	public class FiniteStateAutomaton<TOffset>
	{
		private State<TOffset> _startState;
		private readonly List<State<TOffset>> _acceptingStates;
		private readonly List<State<TOffset>> _states;
		private int _nextTag;
		private readonly Dictionary<string, int> _groups;
		private readonly List<TagMapCommand> _initializers;
		private int _registerCount;
		private readonly Func<Annotation<TOffset>, bool> _filter;
		private readonly Direction _dir;

		public FiniteStateAutomaton(Direction dir)
			: this(dir, ann => true)
		{
		}
		
		public FiniteStateAutomaton(Direction dir, Func<Annotation<TOffset>, bool> filter)
		{
			_initializers = new List<TagMapCommand>();
			_acceptingStates = new List<State<TOffset>>();
			_states = new List<State<TOffset>>();
			_groups = new Dictionary<string, int>();
			_dir = dir;
			_startState = CreateState();
			_filter = filter;
		}

		public IEnumerable<string> GroupNames
		{
			get { return _groups.Keys; }
		}

		public bool GetOffsets(string groupName, NullableValue<TOffset>[,] registers, out TOffset start, out TOffset end)
		{
			int tag = _groups[groupName];
			NullableValue<TOffset> startValue = registers[tag, 0];
			NullableValue<TOffset> endValue = registers[tag + 1, 1];
			if (startValue.HasValue && endValue.HasValue)
			{
				if (_dir == Direction.LeftToRight)
				{
					start = startValue.Value;
					end = endValue.Value;
				}
				else
				{
					start = endValue.Value;
					end = startValue.Value;
				}
				return true;
			}

			start = default(TOffset);
			end = default(TOffset);
			return false;
		}

		private State<TOffset> CreateAcceptingState(PriorityType acceptPriorityType, IEnumerable<AcceptInfo<TOffset>> acceptInfos, IEnumerable<TagMapCommand> finishers)
		{
			var state = new State<TOffset>(_states.Count, acceptPriorityType, acceptInfos, finishers);
			_states.Add(state);
			_acceptingStates.Add(state);
			return state;
		}

		public State<TOffset> CreateAcceptingState(string id, Func<IBidirList<Annotation<TOffset>>, FsaMatch<TOffset>, bool> acceptable, int priority)
		{
			var state = new State<TOffset>(_states.Count, new AcceptInfo<TOffset>(id, acceptable, priority).ToEnumerable());
			_states.Add(state);
			_acceptingStates.Add(state);
			return state;
		}

		public State<TOffset> CreateAcceptingState()
		{
			var state = new State<TOffset>(_states.Count, true);
			_states.Add(state);
			_acceptingStates.Add(state);
			return state;
		}

		public State<TOffset> CreateState()
		{
			var state = new State<TOffset>(_states.Count, false);
			_states.Add(state);
			return state;
		}

		public State<TOffset> CreateTag(State<TOffset> source, State<TOffset> target, string groupName, bool isStart)
		{
			int tag;
			if (isStart)
			{
				if (!_groups.TryGetValue(groupName, out tag))
				{
					tag = _nextTag;
					_nextTag += 2;
					_groups.Add(groupName, tag);
				}
			}
			else
			{
				tag = _groups[groupName] + 1;
			}

			return source.AddArc(target, tag);
		}

		public State<TOffset> StartState
		{
			get
			{
				return _startState;
			}
		}

		public IEnumerable<State<TOffset>> AcceptingStates
		{
			get { return _acceptingStates; }
		}

		public Direction Direction
		{
			get { return _dir; }
		}

		public IEnumerable<State<TOffset>> States
		{
			get { return _states; }
		}

		private class FsaInstance
		{
			private readonly State<TOffset> _state;
			private readonly Annotation<TOffset> _ann;
			private readonly NullableValue<TOffset>[,] _registers;
			private readonly VariableBindings _varBindings;

			public FsaInstance(State<TOffset> state, Annotation<TOffset> ann, NullableValue<TOffset>[,] registers,
				VariableBindings varBindings)
			{
				_state = state;
				_ann = ann;
				_registers = registers;
				_varBindings = varBindings;
			}

			public State<TOffset> State
			{
				get { return _state; }
			}

			public Annotation<TOffset> Annotation
			{
				get { return _ann; }
			}

			public NullableValue<TOffset>[,] Registers
			{
				get { return _registers; }
			}

			public VariableBindings VariableBindings
			{
				get { return _varBindings; }
			}
		}

		public bool IsMatch(IBidirList<Annotation<TOffset>> annList, out IEnumerable<FsaMatch<TOffset>> matches)
		{
			var instStack = new Stack<FsaInstance>();

			var matchList = new List<FsaMatch<TOffset>>();

			Annotation<TOffset> ann = annList.GetFirst(_dir, _filter);

			var registers = new NullableValue<TOffset>[_registerCount, 2];

			var cmds = new List<TagMapCommand>();
			foreach (TagMapCommand cmd in _initializers)
			{
				if (cmd.Dest == 0)
					registers[cmd.Dest, 0].Value = ann.Span.GetStart(_dir);
				else
					cmds.Add(cmd);
			}

			InitializeStack(ann, registers, cmds, instStack);

			while (instStack.Count != 0)
			{
				FsaInstance inst = instStack.Pop();

				foreach (Arc<TOffset> arc in inst.State.OutgoingArcs)
				{
					if (inst.Annotation.FeatureStruct.IsUnifiable(arc.Condition, false, inst.VariableBindings))
					{
						AdvanceFsa(annList, inst.Annotation, inst.Annotation.Span.GetEnd(_dir), inst.Registers, inst.VariableBindings, arc,
							instStack, matchList);
					}
				}
			}

			matchList.Sort();

			if (matchList.Count > 0)
			{
				matches = matchList;
				return true;
			}

			matches = null;
			return false;
		}

		private void InitializeStack(Annotation<TOffset> ann, NullableValue<TOffset>[,] registers, List<TagMapCommand> cmds, Stack<FsaInstance> instStack)
		{
			TOffset offset = ann.Span.GetStart(_dir);

			ExecuteCommands(registers, cmds, new NullableValue<TOffset>(ann.Span.GetStart(_dir)), new NullableValue<TOffset>(),
				ann.Span.GetEnd(_dir));

			for (Annotation<TOffset> a = ann; a != null && a.Span.GetStart(_dir).Equals(offset); a = a.GetNext(_dir, _filter))
			{
				if (a.IsOptional)
				{
					Annotation<TOffset> nextAnn = a.GetNext(_dir, (cur, next) => !cur.Span.Overlaps(next.Span) && _filter(next));
					if (nextAnn != null)
						InitializeStack(nextAnn, registers, cmds, instStack);
				}
			}

			for (; ann != null && ann.Span.GetStart(_dir).Equals(offset); ann = ann.GetNext(_dir, _filter))
			{
				instStack.Push(new FsaInstance(_startState, ann, (NullableValue<TOffset>[,]) registers.Clone(),
					new VariableBindings()));
			}
		}

		private void AdvanceFsa(IBidirList<Annotation<TOffset>> annList, Annotation<TOffset> ann, TOffset end,
			NullableValue<TOffset>[,] registers, VariableBindings varBindings, Arc<TOffset> arc, Stack<FsaInstance> instStack,
			List<FsaMatch<TOffset>> matchList)
		{
			Annotation<TOffset> nextAnn = ann.GetNext(_dir, (cur, next) => !cur.Span.Overlaps(next.Span) && _filter(next));
			TOffset nextOffset = nextAnn == null ? annList.GetLast(_dir, _filter).Span.GetEnd(_dir) : nextAnn.Span.GetStart(_dir);
			var newRegisters = (NullableValue<TOffset>[,]) registers.Clone();
			ExecuteCommands(newRegisters, arc.Commands, new NullableValue<TOffset>(nextOffset), new NullableValue<TOffset>(end),
				ann.Span.GetEnd(_dir));
			if (arc.Target.IsAccepting)
			{
				var matchRegisters = (NullableValue<TOffset>[,]) newRegisters.Clone();
				ExecuteCommands(matchRegisters, arc.Target.Finishers, new NullableValue<TOffset>(), new NullableValue<TOffset>(),
					ann.Span.GetEnd(_dir));
				foreach (AcceptInfo<TOffset> acceptInfo in arc.Target.AcceptInfos)
				{
					var match = new FsaMatch<TOffset>(acceptInfo.ID, matchRegisters, varBindings, acceptInfo.Priority, arc.Target.AcceptPriority);
					if (acceptInfo.Acceptable(annList, match))
						matchList.Add(match);
				}
			}
			if (nextAnn != null)
			{
				for (Annotation<TOffset> a = nextAnn; a != null && a.Span.GetStart(_dir).Equals(nextOffset); a = a.GetNext(_dir, _filter))
				{
					if (a.IsOptional)
						AdvanceFsa(annList, a, end, registers, varBindings, arc, instStack, matchList);
				}

				for (Annotation<TOffset> a = nextAnn; a != null && a.Span.GetStart(_dir).Equals(nextOffset); a = a.GetNext(_dir, _filter))
				{
					instStack.Push(new FsaInstance(arc.Target, a, (NullableValue<TOffset>[,]) newRegisters.Clone(),
						varBindings.Clone()));
				}
			}
		}

		private static void ExecuteCommands(NullableValue<TOffset>[,] registers, IEnumerable<TagMapCommand> cmds,
			NullableValue<TOffset> start, NullableValue<TOffset> end, TOffset curEnd)
		{
			foreach (TagMapCommand cmd in cmds)
			{
				if (cmd.Src == TagMapCommand.CurrentPosition)
				{
					registers[cmd.Dest, 0] = start;
					if (cmd.Dest == 1)
						registers[1, 1].Value = curEnd;
					else
						registers[cmd.Dest, 1] = end;
				}
				else
				{
					registers[cmd.Dest, 0] = registers[cmd.Src, 0];
					registers[cmd.Dest, 1] = registers[cmd.Src, 1];
				}
			}
		}

		private class NfaStateInfo : IEquatable<NfaStateInfo>
		{
			private readonly State<TOffset> _nfsState;
			private readonly Dictionary<int, int> _tags;
			private readonly int _lastPriority;
			private readonly int _maxPriority;

			public NfaStateInfo(State<TOffset> nfaState)
				: this(nfaState, 0, 0, null)
			{
			}

			public NfaStateInfo(State<TOffset> nfaState, int maxPriority, int lastPriority, IDictionary<int, int> tags)
			{
				_nfsState = nfaState;
				_maxPriority = maxPriority;
				_lastPriority = lastPriority;
				_tags = tags == null ? new Dictionary<int, int>() : new Dictionary<int, int>(tags);
			}

			public State<TOffset> NfaState
			{
				get
				{
					return _nfsState;
				}
			}

			public int MaxPriority
			{
				get { return _maxPriority; }
			}

			public int LastPriority
			{
				get { return _lastPriority; }
			}

			public IDictionary<int, int> Tags
			{
				get
				{
					return _tags;
				}
			}

			public override int GetHashCode()
			{
				int tagCode = _tags.Keys.Aggregate(0, (current, tag) => current ^ tag);
				return _nfsState.GetHashCode() ^ tagCode;
			}

			public override bool Equals(object obj)
			{
				if (obj == null)
					return false;
				return Equals(obj as NfaStateInfo);
			}

			public bool Equals(NfaStateInfo other)
			{
				if (other == null)
					return false;

				if (_tags.Count != other._tags.Count)
					return false;

				if (_tags.Keys.Any(tag => !other._tags.ContainsKey(tag)))
					return false;

				return _nfsState.Equals(other._nfsState);
			}

			public override string ToString()
			{
				return string.Format("State {0} ({1}, {2})", _nfsState.Index, _maxPriority, _lastPriority);
			}
		}

		private class SubsetState : IEquatable<SubsetState>
		{
			private readonly HashSet<NfaStateInfo> _nfaStates; 

			public SubsetState(IEnumerable<NfaStateInfo> nfaStates)
			{
				_nfaStates = new HashSet<NfaStateInfo>(nfaStates);
			}

			public IEnumerable<NfaStateInfo> NfaStates
			{
				get { return _nfaStates; }
			}

			public bool IsEmpty
			{
				get { return _nfaStates.Count == 0; }
			}

			public State<TOffset> DfaState { get; set; }


			public override bool Equals(object obj)
			{
				var other = obj as SubsetState;
				return other != null && Equals(other);
			}

			public bool Equals(SubsetState other)
			{
				return other != null && _nfaStates.SetEquals(other._nfaStates);
			}

			public override int GetHashCode()
			{
				return _nfaStates.Aggregate(0, (code, state) => code ^ state.GetHashCode());
			}
		}

		public void MarkArcPriorities()
		{
			// TODO: traverse through the FSA properly
			int nextPriority = 0;
			foreach (Arc<TOffset> arc in from state in _states
										 from arc in state.OutgoingArcs
										 group arc by arc.PriorityType into priorityGroup
										 orderby priorityGroup.Key
										 from arc in priorityGroup
										 select arc)
			{
				arc.Priority = nextPriority++;
			}
		}

		public void Determinize()
		{
			var registerIndices = new Dictionary<int, int>();

			var startState = new NfaStateInfo(_startState);
			var subsetStart = new SubsetState(startState.ToEnumerable());
			subsetStart = EpsilonClosure(subsetStart, subsetStart);

			_states.Clear();
			_acceptingStates.Clear();
			_startState = CreateState();
			subsetStart.DfaState = _startState;

			var cmdTags = new Dictionary<int, int>();
			foreach (NfaStateInfo state in subsetStart.NfaStates)
			{
				foreach (KeyValuePair<int, int> kvp in state.Tags)
					cmdTags[kvp.Key] = kvp.Value;
			}
			_initializers.AddRange(from kvp in cmdTags
								   select new TagMapCommand(GetRegisterIndex(registerIndices, kvp.Key, kvp.Value), TagMapCommand.CurrentPosition));

			var subsetStates = new Dictionary<SubsetState, SubsetState> { {subsetStart, subsetStart} };
			var unmarkedSubsetStates = new Queue<SubsetState>();
			unmarkedSubsetStates.Enqueue(subsetStart);

			while (unmarkedSubsetStates.Count != 0)
			{
				SubsetState curSubsetState = unmarkedSubsetStates.Dequeue();

				FeatureStruct[] conditions = (from state in curSubsetState.NfaStates
										      from tran in state.NfaState.OutgoingArcs
											  where tran.Condition != null
											  select tran.Condition).Distinct().ToArray();
				if (conditions.Length > 0)
				{
					ComputeArcs(subsetStates, unmarkedSubsetStates, registerIndices, curSubsetState, conditions, 0,
						new FeatureStruct[0], new FeatureStruct[0]);
				}
			}

			// TODO: traverse through the FSA properly
			int nextPriority = 0;
			foreach (State<TOffset> state in from state in _acceptingStates
											 group state by state.AcceptPriorityType into priorityGroup
											 orderby priorityGroup.Key
											 from state in priorityGroup.Key == PriorityType.Medium ? priorityGroup.Reverse() : priorityGroup
											 select state)
			{
				state.AcceptPriority = nextPriority++;
			}

			_registerCount = _nextTag + registerIndices.Count;
		}

		private void ComputeArcs(Dictionary<SubsetState, SubsetState> subsetStates, Queue<SubsetState> unmarkedSubsetStates,
			Dictionary<int, int> registerIndices, SubsetState curSubsetState, FeatureStruct[] conditions, int conditionIndex,
			FeatureStruct[] subset1, FeatureStruct[] subset2)
		{
			if (conditionIndex == conditions.Length)
			{
				FeatureStruct condition;
				if (CreateDisjointCondition(subset1, subset2, out condition))
				{
					var cmdTags = new Dictionary<int, int>();
					var reach = new SubsetState(from state in curSubsetState.NfaStates
											    from tran in state.NfaState.OutgoingArcs
												where tran.Condition != null && subset1.Contains(tran.Condition)
												select new NfaStateInfo(tran.Target, Math.Max(tran.Priority, state.MaxPriority), tran.Priority, state.Tags));
					SubsetState target = EpsilonClosure(reach, curSubsetState);
					// this makes the FSA not complete
					if (!target.IsEmpty)
					{
						foreach (NfaStateInfo targetState in target.NfaStates)
						{
							foreach (KeyValuePair<int, int> tag in targetState.Tags)
							{
								bool found = false;
								foreach (NfaStateInfo curState in curSubsetState.NfaStates)
								{
									if (curState.Tags.Contains(tag))
									{
										found = true;
										break;
									}
								}

								if (!found)
									cmdTags[tag.Key] = tag.Value;
							}
						}

						var cmds = (from kvp in cmdTags
						            select new TagMapCommand(GetRegisterIndex(registerIndices, kvp.Key, kvp.Value),
						                                     TagMapCommand.CurrentPosition)).ToList();

						SubsetState subsetState;
						if (subsetStates.TryGetValue(target, out subsetState))
						{
							ReorderTagIndices(target, subsetState, registerIndices, cmds);
							target = subsetState;
						}
						else
						{
							subsetStates.Add(target, target);
							unmarkedSubsetStates.Enqueue(target);
							NfaStateInfo[] sortedStates = target.NfaStates.OrderByDescending(state => state.MaxPriority).ThenByDescending(state => state.LastPriority).ToArray();
							NfaStateInfo[] acceptingStates = sortedStates.Where(state => state.NfaState.IsAccepting).ToArray();
							if (acceptingStates.Length > 0)
							{
								IEnumerable<AcceptInfo<TOffset>> acceptInfos = acceptingStates.SelectMany(state => state.NfaState.AcceptInfos);
								IEnumerable<TagMapCommand> finishers = from state in acceptingStates
																	   from tag in state.Tags.Keys.Distinct()
								                                       let dest = GetRegisterIndex(registerIndices, tag, 0)
								                                       let src = GetRegisterIndex(registerIndices, tag, state.Tags[tag])
								                                       where dest != src && state.Tags[tag] > 0
								                                       select new TagMapCommand(dest, src);
								
								PriorityType priorityType = PriorityType.Medium;
								foreach (Arc<TOffset> arc in sortedStates.Last().NfaState.OutgoingArcs)
								{
									State<TOffset> state = arc.Target;
									while (!state.IsAccepting)
									{
										Arc<TOffset> highestPriArc = state.OutgoingArcs.MinBy(a => a.Priority);
										if (highestPriArc.Condition != null)
											break;
										state = highestPriArc.Target;
									}

									if (state.IsAccepting)
									{
										priorityType = PriorityType.High;
										break;
									}
								}
								target.DfaState = CreateAcceptingState(priorityType, acceptInfos, finishers);
							}
							else
							{
								target.DfaState = CreateState();
							}
						}

						curSubsetState.DfaState.AddArc(condition, target.DfaState, cmds);
					}
				}
			}
			else
			{
				FeatureStruct condition = conditions[conditionIndex];
				ComputeArcs(subsetStates, unmarkedSubsetStates, registerIndices, curSubsetState, conditions, conditionIndex + 1,
					subset1.Concat(condition).ToArray(), subset2);
				ComputeArcs(subsetStates, unmarkedSubsetStates, registerIndices, curSubsetState, conditions, conditionIndex + 1,
					subset1, subset2.Concat(condition).ToArray());
			}
		}

		private bool CreateDisjointCondition(IEnumerable<FeatureStruct> conditions, IEnumerable<FeatureStruct> negConditions, out FeatureStruct result)
		{
			FeatureStruct fs = null;
			foreach (FeatureStruct curCond in conditions)
			{
				if (fs == null)
				{
					fs = curCond;
				}
				else
				{
					if (!fs.Unify(curCond, false, new VariableBindings(), false, out fs))
					{
						result = null;
						return false;
					}
				}
			}

			foreach (FeatureStruct curCond in negConditions)
			{
				FeatureStruct negation;
				if (!curCond.Negation(out negation))
				{
					result = null;
					return false;
				}

				if (fs == null)
				{
					fs = negation;
				}
				else
				{
					if (!fs.Unify(negation, false, new VariableBindings(), false, out fs))
					{
						result = null;
						return false;
					}
				}
			}

			if (fs == null)
			{
				fs = new FeatureStruct();
			}
			else if (!fs.CheckDisjunctiveConsistency(false, new VariableBindings(), out fs))
			{
				result = null;
				return false;
			}

			result = fs;
			return true;
		}

		private void ReorderTagIndices(SubsetState from, SubsetState to, Dictionary<int, int> registerIndices,
			List<TagMapCommand> cmds)
		{
			var newCmds = new List<TagMapCommand>();
			var reorderedIndices = new Dictionary<Tuple<int, int>, int>();
			var reorderedStates = new Dictionary<Tuple<int, int>, NfaStateInfo>();

			foreach (NfaStateInfo fromState in from.NfaStates)
			{
				foreach (NfaStateInfo toState in to.NfaStates)
				{
					if (!toState.NfaState.Equals(fromState.NfaState))
						continue;

					foreach (KeyValuePair<int, int> fromTag in fromState.Tags)
					{
						Tuple<int, int> tagIndex = Tuple.Create(fromTag.Key, toState.Tags[fromTag.Key]);

						int index;
						if (reorderedIndices.TryGetValue(tagIndex, out index))
						{
							NfaStateInfo state = reorderedStates[tagIndex];
							if (index != fromTag.Value && (state.MaxPriority <= fromState.MaxPriority && state.LastPriority <= fromState.LastPriority))
								continue;

							int src = GetRegisterIndex(registerIndices, fromTag.Key, index);
							int dest = GetRegisterIndex(registerIndices, fromTag.Key, tagIndex.Item2);
							newCmds.RemoveAll(cmd => cmd.Src == src && cmd.Dest == dest);
						}

						if (tagIndex.Item2 != fromTag.Value)
						{
							int src = GetRegisterIndex(registerIndices, fromTag.Key, fromTag.Value);
							int dest = GetRegisterIndex(registerIndices, fromTag.Key, tagIndex.Item2);
							newCmds.Add(new TagMapCommand(dest, src));
						}

						reorderedIndices[tagIndex] = fromTag.Value;
						reorderedStates[tagIndex] = fromState;
					}
				}

			}
			cmds.AddRange(newCmds);
		}

		private static SubsetState EpsilonClosure(SubsetState from, SubsetState prev)
		{
			var stack = new Stack<NfaStateInfo>();
			var closure = new Dictionary<int, NfaStateInfo>();
			foreach (NfaStateInfo state in from.NfaStates)
			{
				stack.Push(state);
				closure[state.NfaState.Index] = state;
			}

			while (stack.Count != 0)
			{
				NfaStateInfo topState = stack.Pop();

				foreach (Arc<TOffset> arc in topState.NfaState.OutgoingArcs)
				{
					if (arc.Condition == null)
					{
						int newMaxPriority = Math.Max(arc.Priority, topState.MaxPriority);
						NfaStateInfo temp;
						if (closure.TryGetValue(arc.Target.Index, out temp))
						{
							if (temp.MaxPriority < newMaxPriority)
								continue;
							if (temp.MaxPriority == newMaxPriority && temp.LastPriority <= arc.Priority)
								continue;
						}

						var newState = new NfaStateInfo(arc.Target, newMaxPriority, arc.Priority, topState.Tags);

						if (arc.Tag != -1)
						{
							var indices = new List<int>();
							foreach (NfaStateInfo state in prev.NfaStates)
							{
								int index;
								if (state.Tags.TryGetValue(arc.Tag, out index))
									indices.Add(index);
							}

							int minIndex = 0;
							if (indices.Count > 0)
							{
								indices.Sort();
								for (int i = 0; i <= indices[indices.Count - 1] + 1; i++)
								{
									if (indices.BinarySearch(i) < 0)
									{
										minIndex = i;
										break;
									}
								}
							}

							newState.Tags[arc.Tag] = minIndex;
						}

						closure[arc.Target.Index] = newState;
						stack.Push(newState);
					}
				}
			}

			return new SubsetState(closure.Values);
		}

		private int GetRegisterIndex(Dictionary<int, int> registerIndices, int tag, int index)
		{
			if (index == 0)
				return tag;

			int key = tag ^ index;
			int registerIndex;
			if (registerIndices.TryGetValue(key, out registerIndex))
				return registerIndex;

			registerIndex = _nextTag + registerIndices.Count;
			registerIndices[key] = registerIndex;
			return registerIndex;
		}

		public void ToGraphViz(TextWriter writer)
		{
			writer.WriteLine("digraph G {");

			var stack = new Stack<State<TOffset>>();
			var processed = new HashSet<State<TOffset>>();
			stack.Push(_startState);
			while (stack.Count != 0)
			{
				State<TOffset> state = stack.Pop();
				processed.Add(state);

				writer.Write("  {0} [shape=\"{1}\", color=\"{2}\"", state.Index, state == _startState ? "diamond" : "circle",
					state == _startState ? "green" : state.IsAccepting ? "red" : "black");
				if (state.IsAccepting)
					writer.Write(", peripheries=\"2\"");
				writer.WriteLine("];");

				foreach (Arc<TOffset> arc in state.OutgoingArcs)
				{
					writer.WriteLine("  {0} -> {1} [label=\"{2}\"];", state.Index, arc.Target.Index,
						arc.ToString().Replace("\"", "\\\""));
					if (!processed.Contains(arc.Target) && !stack.Contains(arc.Target))
						stack.Push(arc.Target);
				}
			}

			writer.WriteLine("}");
		}
	}
}
