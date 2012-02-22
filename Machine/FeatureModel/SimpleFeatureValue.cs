using System.Collections.Generic;
using System.Text;

namespace SIL.Machine.FeatureModel
{
	public abstract class SimpleFeatureValue : FeatureValue, ICloneable<SimpleFeatureValue>
	{
		public static implicit operator SimpleFeatureValue(FeatureSymbol symbol)
		{
			return new SymbolicFeatureValue(symbol);
		}

		public static explicit operator FeatureSymbol(SimpleFeatureValue sfv)
		{
			return (FeatureSymbol) ((SymbolicFeatureValue) sfv);
		}

		public static implicit operator SimpleFeatureValue(string str)
		{
			return new StringFeatureValue(str);
		}

		public static explicit operator string(SimpleFeatureValue sfv)
		{
			return (string) ((StringFeatureValue) sfv);
		}

		protected SimpleFeatureValue()
		{
		}

		protected SimpleFeatureValue(string varName, bool agree)
		{
			VariableName = varName;
			Agree = agree;
		}

		protected SimpleFeatureValue(SimpleFeatureValue sfv)
		{
			VariableName = sfv.VariableName;
			Agree = sfv.Agree;
		}

		public string VariableName { get; protected set; }

		public bool Agree { get; protected set; }

		public bool IsVariable
		{
			get { return !string.IsNullOrEmpty(VariableName); }
		}

		internal override bool IsDefiniteUnifiable(FeatureValue other, bool useDefaults, VariableBindings varBindings)
		{
			SimpleFeatureValue otherSfv;
			if (!Dereference(other, out otherSfv))
				return false;

			if (!IsVariable && !otherSfv.IsVariable)
			{
				if (!Overlaps(false, otherSfv, false))
					return false;
			}
			else if (IsVariable && !otherSfv.IsVariable)
			{
				SimpleFeatureValue binding;
				if (varBindings.TryGetValue(VariableName, out binding))
				{
					if (!binding.Overlaps(!Agree, otherSfv, false))
						return false;
				}
				else
				{
					varBindings[VariableName] = otherSfv.GetVariableValue(Agree);
				}
			}
			else if (!IsVariable && otherSfv.IsVariable)
			{
				SimpleFeatureValue binding;
				if (varBindings.TryGetValue(otherSfv.VariableName, out binding))
				{
					if (!Overlaps(false, binding, !otherSfv.Agree))
						return false;
				}
				else
				{
					varBindings[otherSfv.VariableName] = GetVariableValue(otherSfv.Agree);
				}
			}
			else
			{
				if (VariableName != otherSfv.VariableName || Agree != otherSfv.Agree)
					return false;
			}

			return true;
		}

		internal override bool DestructiveUnify(FeatureValue other, bool useDefaults, bool preserveInput, IDictionary<FeatureValue, FeatureValue> copies, VariableBindings varBindings)
		{
			SimpleFeatureValue otherSfv;
			if (!Dereference(other, out otherSfv))
				return false;

			if (this == otherSfv)
				return true;

			if (!IsVariable && !otherSfv.IsVariable)
			{
				if (!Overlaps(false, otherSfv, false))
					return false;
				IntersectWith(false, otherSfv, false);
			}
			else if (IsVariable && !otherSfv.IsVariable)
			{
				SimpleFeatureValue binding;
				if (varBindings.TryGetValue(VariableName, out binding))
				{
					if (!binding.Overlaps(!Agree, otherSfv, false))
						return false;
					UnionWith(false, binding, !Agree);
					IntersectWith(false, otherSfv, false);
				}
				else
				{
					UnionWith(false, otherSfv, false);
					varBindings[VariableName] = otherSfv.GetVariableValue(Agree);
				}
				VariableName = null;
			}
			else if (!IsVariable && otherSfv.IsVariable)
			{
				SimpleFeatureValue binding;
				if (varBindings.TryGetValue(otherSfv.VariableName, out binding))
				{
					if (!Overlaps(false, binding, !otherSfv.Agree))
						return false;
					IntersectWith(false, binding, !otherSfv.Agree);
				}
				else
				{
					varBindings[otherSfv.VariableName] = GetVariableValue(otherSfv.Agree);
				}
			}
			else
			{
				if (VariableName != otherSfv.VariableName || Agree != otherSfv.Agree)
					return false;
			}

			if (preserveInput)
			{
				if (copies != null)
					copies[otherSfv] = this;
			}
			else
			{
				otherSfv.Forward = this;
			}

			return true;
		}

		internal override bool Union(FeatureValue other, VariableBindings varBindings, IDictionary<FeatureStruct, ISet<FeatureStruct>> visited)
		{
			SimpleFeatureValue otherSfv;
			if (!Dereference(other, out otherSfv))
				return true;

			if (!IsVariable && !otherSfv.IsVariable)
			{
				UnionWith(false, otherSfv, false);
			}
			else if (IsVariable && !otherSfv.IsVariable)
			{
				SimpleFeatureValue binding;
				if (varBindings.TryGetValue(VariableName, out binding))
				{
					UnionWith(false, binding, !Agree);
					UnionWith(false, otherSfv, false);
				}
				else
				{
					UnionWith(false, otherSfv, false);
					varBindings[VariableName] = otherSfv.GetVariableValue(Agree);
				}
				VariableName = null;
			}
			else if (!IsVariable && otherSfv.IsVariable)
			{
				SimpleFeatureValue binding;
				if (varBindings.TryGetValue(otherSfv.VariableName, out binding))
					UnionWith(false, binding, !otherSfv.Agree);
				else
					varBindings[otherSfv.VariableName] = GetVariableValue(otherSfv.Agree);
			}
			else
			{
				SimpleFeatureValue binding;
				if (varBindings.TryGetValue(VariableName, out binding))
				{
					UnionWith(false, binding, !Agree);
					SimpleFeatureValue otherBinding;
					if (varBindings.TryGetValue(otherSfv.VariableName, out otherBinding))
						UnionWith(false, otherBinding, !otherSfv.Agree);
					VariableName = null;
				}
				else
				{
					SimpleFeatureValue otherBinding;
					if (varBindings.TryGetValue(otherSfv.VariableName, out otherBinding))
					{
						UnionWith(false, otherBinding, !otherSfv.Agree);
						VariableName = null;
					}
					else
					{
						return VariableName == otherSfv.VariableName && Agree == otherSfv.Agree;
					}
				}
			}

			return !IsUninstantiated;
		}

		internal override bool Subtract(FeatureValue other, VariableBindings varBindings, IDictionary<FeatureStruct, ISet<FeatureStruct>> visited)
		{
			SimpleFeatureValue otherSfv;
			if (!Dereference(other, out otherSfv))
				return true;

			if (!IsVariable && !otherSfv.IsVariable)
			{
				ExceptWith(false, otherSfv, false);
			}
			else if (IsVariable && !otherSfv.IsVariable)
			{
				SimpleFeatureValue binding;
				if (varBindings.TryGetValue(VariableName, out binding))
				{
					UnionWith(false, binding, !Agree);
					ExceptWith(false, otherSfv, false);
					VariableName = null;
				}
			}
			else if (!IsVariable && otherSfv.IsVariable)
			{
				SimpleFeatureValue binding;
				if (varBindings.TryGetValue(otherSfv.VariableName, out binding))
					ExceptWith(false, binding, !otherSfv.Agree);
			}
			else
			{
				SimpleFeatureValue binding;
				if (varBindings.TryGetValue(VariableName, out binding))
				{
					UnionWith(false, binding, !Agree);
					SimpleFeatureValue otherBinding;
					if (varBindings.TryGetValue(otherSfv.VariableName, out otherBinding))
						ExceptWith(false, otherSfv, false);
					VariableName = null;
				}
				else if (!varBindings.ContainsVariable(otherSfv.VariableName))
				{
					return VariableName != otherSfv.VariableName || Agree != otherSfv.Agree;
				}
			}

			return IsSatisfiable;
		}

		internal SimpleFeatureValue GetVariableValue(bool agree)
		{
			return agree ? Clone() : Negation();
		}

		protected override bool NondestructiveUnify(FeatureValue other, bool useDefaults, IDictionary<FeatureValue, FeatureValue> copies,
			VariableBindings varBindings, out FeatureValue output)
		{
			FeatureValue copy = Clone();
			copies[this] = copy;
			copies[other] = copy;
			if (!copy.DestructiveUnify(other, useDefaults, true, copies, varBindings))
			{
				output = null;
				return false;
			}
			output = copy;
			return true;
		}

		internal override FeatureValue Clone(IDictionary<FeatureValue, FeatureValue> copies)
		{
			FeatureValue copy;
			if (copies != null)
			{
				if (copies.TryGetValue(this, out copy))
					return copy;
			}

			copy = Clone();

			if (copies != null)
				copies[this] = copy;
			return copy;
		}

		public abstract SimpleFeatureValue Clone();

		internal override bool Negation(IDictionary<FeatureValue, FeatureValue> visited, out FeatureValue output)
		{
			FeatureValue negation;
			if (visited.TryGetValue(this, out negation))
			{
				output = negation;
				return true;
			}

			output = Negation();

			visited[this] = output;
			return true;
		}

		public abstract SimpleFeatureValue Negation();

		internal override void FindReentrances(IDictionary<FeatureValue, bool> reentrances)
		{
			reentrances[this] = reentrances.ContainsKey(this);
		}

		internal override void GetAllValues(ISet<FeatureValue> values, bool indefinite)
		{
			values.Add(this);
		}

		internal override string ToString(ISet<FeatureValue> visited, IDictionary<FeatureValue, int> reentranceIds)
		{
			if (visited.Contains(this))
				return string.Format("<{0}>", reentranceIds[this]);

			visited.Add(this);
			var sb = new StringBuilder();
			int id;
			if (reentranceIds.TryGetValue(this, out id))
				sb.AppendFormat("<{0}>=", id);
			sb.Append(ToString());
			return sb.ToString();
		}

		internal override int GetHashCode(ISet<FeatureValue> visited)
		{
			if (visited.Contains(this))
				return 1;
			visited.Add(this);

			return GetHashCode();
		}

		internal override bool Equals(FeatureValue other, ISet<FeatureValue> visitedSelf, ISet<FeatureValue> visitedOther, IDictionary<FeatureValue, FeatureValue> visitedPairs)
		{
			if (other == null)
				return false;

			SimpleFeatureValue otherSfv;
			if (!Dereference(other, out otherSfv))
				return false;

			if (this == otherSfv)
				return true;

			if (visitedSelf.Contains(this) || visitedOther.Contains(otherSfv))
			{
				FeatureValue fv;
				if (visitedPairs.TryGetValue(this, out fv))
					return fv == otherSfv;
				return false;
			}

			visitedSelf.Add(this);
			visitedOther.Add(otherSfv);
			visitedPairs[this] = otherSfv;

			return Equals(otherSfv);
		}

		protected abstract bool Overlaps(bool not, SimpleFeatureValue other, bool notOther);
		protected abstract void IntersectWith(bool not, SimpleFeatureValue other, bool notOther);
		protected abstract void UnionWith(bool not, SimpleFeatureValue other, bool notOther);
		protected abstract void ExceptWith(bool not, SimpleFeatureValue other, bool notOther);
		protected virtual bool IsSatisfiable
		{
			get { return IsVariable; }
		}
		protected virtual bool IsUninstantiated
		{
			get { return !IsVariable; }
		}
	}
}