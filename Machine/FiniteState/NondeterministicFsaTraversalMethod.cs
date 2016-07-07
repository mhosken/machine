﻿using System;
using System.Collections.Generic;
using SIL.Collections;
using SIL.Machine.Annotations;
using SIL.Machine.FeatureModel;

namespace SIL.Machine.FiniteState
{
	internal class NondeterministicFsaTraversalMethod<TData, TOffset> : TraversalMethodBase<TData, TOffset, NondeterministicFsaTraversalInstance<TData, TOffset>> where TData : IAnnotatedData<TOffset>
	{
		public NondeterministicFsaTraversalMethod(IEqualityComparer<NullableValue<TOffset>[,]> registersEqualityComparer, int registerCount, Direction dir,
			Func<Annotation<TOffset>, bool> filter, State<TData, TOffset> startState, TData data, bool endAnchor, bool unification, bool useDefaults, bool ignoreVariables)
			: base(registersEqualityComparer, registerCount, dir, filter, startState, data, endAnchor, unification, useDefaults, ignoreVariables)
		{
		}

		public override IEnumerable<FstResult<TData, TOffset>> Traverse(ref int annIndex, NullableValue<TOffset>[,] initRegisters, IList<TagMapCommand> initCmds, ISet<int> initAnns)
		{
			Stack<NondeterministicFsaTraversalInstance<TData, TOffset>> instStack = InitializeStack(ref annIndex, initRegisters, initCmds, initAnns);

			var curResults = new List<FstResult<TData, TOffset>>();
			var traversed = new HashSet<Tuple<State<TData, TOffset>, int, NullableValue<TOffset>[,]>>(
				AnonymousEqualityComparer.Create<Tuple<State<TData, TOffset>, int, NullableValue<TOffset>[,]>>(KeyEquals, KeyGetHashCode));
			while (instStack.Count != 0)
			{
				NondeterministicFsaTraversalInstance<TData, TOffset> inst = instStack.Pop();

				bool releaseInstance = true;
				VariableBindings varBindings = null;
				int i = 0;
				foreach (Arc<TData, TOffset> arc in inst.State.Arcs)
				{
					bool isInstReusable = i == inst.State.Arcs.Count - 1;
					if (arc.Input.IsEpsilon)
					{
						if (!inst.Visited.Contains(arc.Target))
						{
							NondeterministicFsaTraversalInstance<TData, TOffset> ti;
							if (isInstReusable)
							{
								ti = inst;
							}
							else
							{
								ti = CopyInstance(inst);
								if (inst.VariableBindings != null && varBindings == null)
									varBindings = inst.VariableBindings.DeepClone();
								ti.VariableBindings = varBindings;
							}

							ti.Visited.Add(arc.Target);
							NondeterministicFsaTraversalInstance<TData, TOffset> newInst = EpsilonAdvance(ti, arc, curResults);
							Tuple<State<TData, TOffset>, int, NullableValue<TOffset>[,]> key = Tuple.Create(newInst.State, newInst.AnnotationIndex, newInst.Registers);
							if (!traversed.Contains(key))
							{
								instStack.Push(newInst);
								traversed.Add(key);
							}
							if (isInstReusable)
								releaseInstance = false;
							varBindings = null;
						}
					}
					else
					{
						if (inst.VariableBindings != null && varBindings == null)
							varBindings = isInstReusable ? inst.VariableBindings : inst.VariableBindings.DeepClone();
						if (CheckInputMatch(arc, inst.AnnotationIndex, varBindings))
						{
							NondeterministicFsaTraversalInstance<TData, TOffset> ti = isInstReusable ? inst : CopyInstance(inst);

							foreach (NondeterministicFsaTraversalInstance<TData, TOffset> newInst in Advance(ti, varBindings, arc, curResults))
							{
								newInst.Visited.Clear();
								Tuple<State<TData, TOffset>, int, NullableValue<TOffset>[,]> key = Tuple.Create(newInst.State, newInst.AnnotationIndex, newInst.Registers);
								if (!traversed.Contains(key))
								{
									instStack.Push(newInst);
									traversed.Add(key);
								}
							}
							if (isInstReusable)
								releaseInstance = false;
							varBindings = null;
						}
					}
					i++;
				}

				if (releaseInstance)
					ReleaseInstance(inst);
			}

			return curResults;
		}

		protected override NondeterministicFsaTraversalInstance<TData, TOffset> CreateInstance(int registerCount, bool ignoreVariables)
		{
			return new NondeterministicFsaTraversalInstance<TData, TOffset>(registerCount, ignoreVariables);
		}

		private bool KeyEquals(Tuple<State<TData, TOffset>, int, NullableValue<TOffset>[,]> x, Tuple<State<TData, TOffset>, int, NullableValue<TOffset>[,]> y)
		{
			return x.Item1.Equals(y.Item1) && x.Item2.Equals(y.Item2) && RegistersEqualityComparer.Equals(x.Item3, y.Item3);
		}

		private int KeyGetHashCode(Tuple<State<TData, TOffset>, int, NullableValue<TOffset>[,]> m)
		{
			int code = 23;
			code = code * 31 + m.Item1.GetHashCode();
			code = code * 31 + m.Item2.GetHashCode();
			code = code * 31 + RegistersEqualityComparer.GetHashCode(m.Item3);
			return code;
		}

		private Stack<NondeterministicFsaTraversalInstance<TData, TOffset>> InitializeStack(ref int annIndex, NullableValue<TOffset>[,] registers,
			IList<TagMapCommand> cmds, ISet<int> initAnns)
		{
			var instStack = new Stack<NondeterministicFsaTraversalInstance<TData, TOffset>>();
			foreach (NondeterministicFsaTraversalInstance<TData, TOffset> inst in Initialize(ref annIndex, registers, cmds, initAnns))
				instStack.Push(inst);
			return instStack;
		}
	}
}