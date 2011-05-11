﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SIL.APRE.Fsa;

namespace SIL.APRE
{
	/// <summary>
	/// This enumeration represents the morpher mode type.
	/// </summary>
	public enum ModeType
	{
		/// <summary>
		/// Analysis mode (unapplication of rules)
		/// </summary>
		Analysis,
		/// <summary>
		/// Synthesis mode (application of rules)
		/// </summary>
		Synthesis
	}

	public sealed class Pattern<TOffset> : BidirList<PatternNode<TOffset>>, ICloneable
	{
		private readonly SpanFactory<TOffset> _spanFactory;
		private readonly bool _checkSynthesisClean;
		private readonly bool _checkAnalysisClean;
		private readonly HashSet<string> _synthesisTypes;
		private readonly HashSet<string> _analysisTypes;
		private FiniteStateAutomaton<TOffset, FeatureStructure> _rightToLeftFsa;
		private FiniteStateAutomaton<TOffset, FeatureStructure> _leftToRightFsa;

        /// <summary>
		/// Initializes a new instance of the <see cref="Pattern&lt;TOffset&gt;"/> class.
        /// </summary>
        public Pattern(SpanFactory<TOffset> spanFactory)
			: this(spanFactory, (IEnumerable<string>) null)
        {
        }

		public Pattern(SpanFactory<TOffset> spanFactory, IEnumerable<string> types)
			: this(spanFactory, types, types)
		{
		}

		public Pattern(SpanFactory<TOffset> spanFactory, IEnumerable<string> synthesisTypes, IEnumerable<string> analysisTypes)
			: this(spanFactory, synthesisTypes, analysisTypes, false, false)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Pattern&lt;TOffset&gt;"/> class.
		/// </summary>
		/// <param name="spanFactory"></param>
		/// <param name="synthesisTypes"></param>
		/// <param name="analysisTypes"></param>
		/// <param name="checkSynthesisClean"></param>
		/// <param name="checkAnalysisClean"></param>
		public Pattern(SpanFactory<TOffset> spanFactory, IEnumerable<string> synthesisTypes, IEnumerable<string> analysisTypes,
			bool checkSynthesisClean, bool checkAnalysisClean)
		{
			_spanFactory = spanFactory;
			if (synthesisTypes != null)
				_synthesisTypes = new HashSet<string>(synthesisTypes);
			if (analysisTypes != null)
				_analysisTypes = new HashSet<string>(analysisTypes);
			_checkSynthesisClean = checkSynthesisClean;
			_checkAnalysisClean = checkAnalysisClean;
        }

		public Pattern(SpanFactory<TOffset> spanFactory, params PatternNode<TOffset>[] nodes)
			: this(spanFactory, null, nodes)
		{
		}

		public Pattern(SpanFactory<TOffset> spanFactory, IEnumerable<string> types, params PatternNode<TOffset>[] nodes)
			: this(spanFactory, types, types, nodes)
		{
		}

		public Pattern(SpanFactory<TOffset> spanFactory, IEnumerable<string> synthesisTypes, IEnumerable<string> analysisTypes,
			params PatternNode<TOffset>[] nodes)
			: this(spanFactory, synthesisTypes, analysisTypes, false, false, nodes)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Pattern&lt;TOffset&gt;"/> class.
		/// </summary>
		/// <param name="spanFactory"></param>
		/// <param name="synthesisTypes"></param>
		/// <param name="analysisTypes"></param>
		/// <param name="checkSynthesisClean"></param>
		/// <param name="checkAnalysisClean"></param>
		/// <param name="nodes"></param>
		public Pattern(SpanFactory<TOffset> spanFactory, IEnumerable<string> synthesisTypes, IEnumerable<string> analysisTypes,
			bool checkSynthesisClean, bool checkAnalysisClean, params PatternNode<TOffset>[] nodes)
			: this(spanFactory, synthesisTypes, analysisTypes, checkSynthesisClean, checkAnalysisClean)
		{
			AddMany(nodes);
		}

        /// <summary>
        /// Copy constructor.
        /// </summary>
        /// <param name="pattern">The phonetic pattern.</param>
        public Pattern(Pattern<TOffset> pattern)
        {
        	_spanFactory = pattern._spanFactory;
			_checkSynthesisClean = pattern._checkSynthesisClean;
			_checkAnalysisClean = pattern._checkAnalysisClean;
			if (_synthesisTypes != null)
				_synthesisTypes = new HashSet<string>(pattern._synthesisTypes);
			if (_analysisTypes != null)
				_analysisTypes = new HashSet<string>(pattern._analysisTypes);
			AddMany(pattern.Select(node => node.Clone()));
        }

        /// <summary>
        /// Gets all of the features referenced in this pattern. 
        /// </summary>
        /// <value>The features.</value>
        public IEnumerable<Feature> Features
        {
            get
            {
                var features = new HashSet<Feature>();
                foreach (PatternNode<TOffset> node in this)
                    features.UnionWith(node.Features);
                return features;
            }
        }

        /// <summary>
        /// Determines whether the phonetic pattern references the specified feature.
        /// </summary>
        /// <param name="feature">The feature.</param>
        /// <returns>
        /// 	<c>true</c> if the specified feature is referenced, otherwise <c>false</c>.
        /// </returns>
        public bool IsFeatureReferenced(Feature feature)
        {
        	return this.Any(node => node.IsFeatureReferenced(feature));
        }

		public bool CheckClean(ModeType mode)
		{
			return mode == ModeType.Synthesis ? _checkSynthesisClean : _checkAnalysisClean;
		}

		public FiniteStateAutomaton<TOffset, FeatureStructure> GetFsa(Direction dir)
		{
			return dir == Direction.LeftToRight ? _leftToRightFsa : _rightToLeftFsa;
		}

		private State<TOffset, FeatureStructure> GenerateNfa(FiniteStateAutomaton<TOffset, FeatureStructure> fsa,
			State<TOffset, FeatureStructure> startState, Direction dir)
		{
			return GetFirst(dir).GenerateNfa(fsa, startState, dir);
		}

		public void Compile()
		{
			_leftToRightFsa = new FiniteStateAutomaton<TOffset, FeatureStructure>(GenerateNfa, Direction.LeftToRight, _synthesisTypes, _analysisTypes);
			_rightToLeftFsa = new FiniteStateAutomaton<TOffset, FeatureStructure>(GenerateNfa, Direction.RightToLeft, _synthesisTypes, _analysisTypes);
		}

		public bool IsMatch(IBidirList<Annotation<TOffset>> annList, Direction dir, ModeType mode)
		{
			return IsMatch(annList, dir, mode, null);
		}

		public bool IsMatch(IBidirList<Annotation<TOffset>> annList, Direction dir, ModeType mode, FeatureStructure varValues)
		{
			IList<PatternMatch<TOffset>> matches;
			return IsMatch(annList, dir, mode, varValues, false, out matches);
		}

		public bool IsMatch(IBidirList<Annotation<TOffset>> annList, Direction dir, ModeType mode, out PatternMatch<TOffset> match)
		{
			return IsMatch(annList, dir, mode, null, out match);
		}

		public bool IsMatch(IBidirList<Annotation<TOffset>> annList, Direction dir, ModeType mode, FeatureStructure varValues,
			out PatternMatch<TOffset> match)
		{
			IList<PatternMatch<TOffset>> matches;
			if (IsMatch(annList, dir, mode, varValues, false, out matches))
			{
				match = matches[0];
				return true;
			}

			match = null;
			return false;
		}

		public bool IsMatch(IBidirList<Annotation<TOffset>> annList, Direction dir, ModeType mode,
			out IList<PatternMatch<TOffset>> matches)
		{
			return IsMatch(annList, dir, mode, null, out matches);
		}

		public bool IsMatch(IBidirList<Annotation<TOffset>> annList, Direction dir, ModeType mode, FeatureStructure varValues,
			out IList<PatternMatch<TOffset>> matches)
		{
			return IsMatch(annList, dir, mode, varValues, true, out matches);
		}

		private bool IsMatch(IBidirList<Annotation<TOffset>> annList, Direction dir, ModeType mode, FeatureStructure varValues,
			bool allMatches, out IList<PatternMatch<TOffset>> matches)
		{
			FiniteStateAutomaton<TOffset, FeatureStructure> fsa;
			if (dir == Direction.LeftToRight)
			{
				if (_leftToRightFsa == null)
					_leftToRightFsa = new FiniteStateAutomaton<TOffset, FeatureStructure>(GenerateNfa, Direction.LeftToRight, _synthesisTypes, _analysisTypes);
				fsa = _leftToRightFsa;
			}
			else
			{
				if (_rightToLeftFsa == null)
					_rightToLeftFsa = new FiniteStateAutomaton<TOffset, FeatureStructure>(GenerateNfa, Direction.RightToLeft, _synthesisTypes, _analysisTypes);
				fsa = _rightToLeftFsa;
			}

			IEnumerable<FsaMatch<TOffset, FeatureStructure>> fsaMatches;
			if (fsa.IsMatch(annList, mode, varValues, allMatches, out fsaMatches))
			{
				matches = new List<PatternMatch<TOffset>>();
				foreach (FsaMatch<TOffset, FeatureStructure> match in fsaMatches)
				{
					var groups = new Dictionary<int, Span<TOffset>>();
					TOffset matchStart, matchEnd;
					fsa.GetOffsets(0, match, out matchStart, out matchEnd);
					var matchSpan = _spanFactory.Create(matchStart, matchEnd);
					groups[0] = matchSpan;

					foreach (int group in fsa.Groups)
					{
						if (group == 0)
							continue;

						TOffset start, end;
						if (fsa.GetOffsets(group, match, out start, out end))
						{
							if (_spanFactory.IsValidSpan(start, end))
							{
								Span<TOffset> span = _spanFactory.Create(start, end);
								if (matchSpan.Contains(span))
									groups[group] = span;
							}
						}
					}

					matches.Add(new PatternMatch<TOffset>(groups, match.Data));
				}
				return true;
			}

			matches = null;
			return false;
		}

        object ICloneable.Clone()
        {
            return Clone();
        }

        public Pattern<TOffset> Clone()
        {
            return new Pattern<TOffset>(this);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (PatternNode<TOffset> node in this)
                sb.Append(node.ToString());
            return sb.ToString();
        }
	}
}
