﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SIL.Machine.Annotations;
using SIL.Machine.DataStructures;
using SIL.Machine.FeatureModel;
using SIL.Machine.Matching;
using SIL.ObjectModel;

namespace SIL.Machine.HermitCrab
{
	public static class HermitCrabExtensions
	{
		public static FeatureSymbol Type(this ShapeNode node)
		{
			return (FeatureSymbol) node.Annotation.FeatureStruct.GetValue(HCFeatureSystem.Type);
		}

		public static FeatureSymbol Type(this Annotation<ShapeNode> ann)
		{
			return (FeatureSymbol) ann.FeatureStruct.GetValue(HCFeatureSystem.Type);
		}

		public static FeatureSymbol Type(this Constraint<Word, ShapeNode> constraint)
		{
			return (FeatureSymbol) constraint.FeatureStruct.GetValue(HCFeatureSystem.Type);
		}

		internal static FeatureStruct AntiFeatureStruct(this FeatureStruct fs)
		{
			// TODO: handle reentrancy properly

			var result = new FeatureStruct();
			foreach (Feature feature in fs.Features)
			{
				FeatureValue value = fs.GetValue(feature);
				var childFS = value as FeatureStruct;
				FeatureValue newValue;
				if (childFS != null)
				{
					newValue = HCFeatureSystem.Instance.ContainsFeature(feature) ? childFS.Clone() : childFS.AntiFeatureStruct();
				}
				else
				{
					var childSfv = (SimpleFeatureValue) value;
					newValue = HCFeatureSystem.Instance.ContainsFeature(feature) ? childSfv.Clone() : childSfv.Negation();
				}
				result.AddValue(feature, newValue);
			}
			return result;
		}

		internal static bool IsDirty(this ShapeNode node)
		{
			return ((FeatureSymbol) node.Annotation.FeatureStruct.GetValue(HCFeatureSystem.Modified)) == HCFeatureSystem.Dirty;
		}

		internal static void SetDirty(this ShapeNode node, bool dirty)
		{
			node.Annotation.FeatureStruct.AddValue(HCFeatureSystem.Modified, dirty ? HCFeatureSystem.Dirty : HCFeatureSystem.Clean);
		}

		internal static bool IsDeleted(this Annotation<ShapeNode> ann)
		{
			SymbolicFeatureValue sfv;
			if (ann.FeatureStruct.TryGetValue(HCFeatureSystem.Deletion, out sfv))
				return ((FeatureSymbol) sfv) == HCFeatureSystem.Deleted;
			return false;
		}

		internal static bool IsDeleted(this ShapeNode node)
		{
			return node.Annotation.IsDeleted();
		}

		internal static void SetDeleted(this ShapeNode node, bool deleted)
		{
			node.Annotation.FeatureStruct.AddValue(HCFeatureSystem.Deletion, deleted ? HCFeatureSystem.Deleted : HCFeatureSystem.NotDeleted);
		}

		private static readonly IEqualityComparer<ShapeNode> NodeComparer = new ProjectionEqualityComparer<ShapeNode, FeatureStruct>(node => node.Annotation.FeatureStruct,
			FreezableEqualityComparer<FeatureStruct>.Default);
		internal static bool Duplicates(this Shape x, Shape y)
		{
			return x.Where(n => !n.Annotation.Optional).SequenceEqual(y.Where(n => !n.Annotation.Optional), NodeComparer);
		}

		internal static IEnumerable<Word> RemoveDuplicates(this IEnumerable<Word> words)
		{
			var output = new List<Word>();
			foreach (Word word in words)
			{
				// check to see if this is a duplicate of another output analysis, this is not strictly necessary, but
				// it helps to reduce the search space
				bool add = true;
				for (int i = 0; i < output.Count; i++)
				{
					if (word.Shape.Duplicates(output[i].Shape))
					{
						if (word.Shape.Count > output[i].Shape.Count)
							// if this is a duplicate and it is longer, then use this analysis and remove the previous one
							output.RemoveAt(i);
						else
							// if it is shorter, then do not add it to the output list
							add = false;
						break;
					}
				}

				if (add)
					output.Add(word);
			}
			return output;
		}

		/// <summary>
		/// Converts the specified phonetic shape to a valid regular expression string. Regular expressions
		/// formatted for display purposes are NOT guaranteed to compile.
		/// </summary>
		/// <param name="shape">The phonetic shape.</param>
		/// <param name="table">The symbol table.</param>
		/// <param name="displayFormat">if <c>true</c> the result will be formatted for display, otherwise
		/// it will be formatted for compilation.</param>
		/// <returns>The regular expression string.</returns>
		public static string ToRegexString(this Shape shape, SymbolTable table, bool displayFormat)
		{
			var sb = new StringBuilder();
			if (!displayFormat)
				sb.Append("^");
			foreach (ShapeNode node in shape)
			{
				if (node.IsDeleted())
					continue;

				string[] strReps = table.GetMatchingStrReps(node).ToArray();
				int strRepCount = strReps.Length;
				if (strRepCount > 0)
				{
					if (strRepCount > 1)
						sb.Append(displayFormat ? "[" : "(");
					int i = 0;
					foreach (string strRep in strReps)
					{
						if (strRep.Length > 1)
							sb.Append("(");

						sb.Append(displayFormat ? strRep : Regex.Escape(strRep));

						if (strRep.Length > 1)
							sb.Append(")");
						if (i < strRepCount - 1 && !displayFormat)
							sb.Append("|");
						i++;
					}
					if (strReps.Length > 1)
						sb.Append(displayFormat ? "]" : ")");

					if (node.Annotation.Optional)
						sb.Append("?");
				}
			}
			if (!displayFormat)
				sb.Append("$");
			return sb.ToString();
		}

		public static string ToString(this IEnumerable<ShapeNode> nodes, SymbolTable table, bool includeBdry)
		{
			var sb = new StringBuilder();
			foreach (ShapeNode node in nodes)
			{
				if ((!includeBdry && node.Annotation.Type() == HCFeatureSystem.Boundary) || node.IsDeleted())
					continue;

				IEnumerable<string> strReps = table.GetMatchingStrReps(node);
				string strRep = strReps.FirstOrDefault();
				if (strRep != null)
					sb.Append(strRep);
			}
			return sb.ToString();
		}
	}
}