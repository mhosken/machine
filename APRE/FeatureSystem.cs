using System;
using System.Collections.Generic;
using System.Linq;

namespace SIL.APRE
{
	public struct FeatValPair
	{
		private readonly object _feature;
		private readonly IEnumerable<object> _values;

		public FeatValPair(object feature, params object[] values)
			: this(feature, (IEnumerable<object>) values)
		{
		}

		public FeatValPair(object feature, IEnumerable<object> values)
		{
			_feature = feature;
			_values = values;
		}

		public object Feature
		{
			get { return _feature; }
		}

		public IEnumerable<object> Values
		{
			get { return _values; }
		}
	}

	public struct FeatStruc
	{
		private readonly IEnumerable<object> _contents;

		public FeatStruc(params object[] contents)
		{
			_contents = contents;
		}

		public FeatStruc(IEnumerable<object> contents)
		{
			_contents = contents;
		}

		public IEnumerable<object> Contents
		{
			get { return _contents; }
		}
	}

    /// <summary>
    /// This class represents a feature system. It encapsulates all of the valid features and symbols.
    /// </summary>
    public class FeatureSystem
    {
    	private readonly IDBearerSet<Feature> _features;
    	private readonly IDBearerSet<FeatureSymbol> _symbols;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureSystem"/> class.
        /// </summary>
        public FeatureSystem()
        {
            _features = new IDBearerSet<Feature>();
            _symbols = new IDBearerSet<FeatureSymbol>();
        }

        /// <summary>
        /// Gets the features.
        /// </summary>
        /// <value>The features.</value>
        public IEnumerable<Feature> Features
        {
            get
            {
                return _features;
            }
        }

        public int NumFeatures
        {
            get
            {
                return _features.Count;
            }
        }

        /// <summary>
        /// Gets the feature values.
        /// </summary>
        /// <value>The values.</value>
        public IEnumerable<FeatureSymbol> Symbols
        {
            get
            {
                return _symbols;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this feature system has features.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance has features, otherwise <c>false</c>.
        /// </value>
        public bool HasFeatures
        {
            get
            {
                return _features.Count > 0;
            }
        }

		public FeatureStructure CreateFeatureStructure(params object[] contents)
		{
			return GenerateFeatureStructure(contents);
		}

		public FeatureStructure CreateFeatureStructure(IEnumerable<object> contents)
		{
			return GenerateFeatureStructure(contents);
		}

		private FeatureStructure GenerateFeatureStructure(IEnumerable<object> contents)
		{
			var featureValues = new Dictionary<Feature, FeatureValue>();
			foreach (object obj in contents)
			{
				if (obj is FeatValPair)
				{
					var pair = (FeatValPair) obj;

					Feature f;
					if (pair.Feature is Feature)
					{
						f = (Feature) pair.Feature;
					}
					else if (pair.Feature is string)
					{
						var id = (string) pair.Feature;
						if (!_features.TryGetValue(id, out f))
							f = null;
					}
					else
					{
						throw new ArgumentException("An invalid feature was specified.", "contents");
					}

					FeatureValue v;
					if (pair.Values.Count() == 1)
					{
						object valueObj = pair.Values.First();
						if (valueObj is FeatureSymbol)
						{
							var symbol = (FeatureSymbol) valueObj;
							if (symbol.Feature != f)
								throw new ArgumentException("A specified symbol is not associated with the specified feature.", "contents");
							v = new SymbolicFeatureValue(symbol);
						}
						else if (valueObj is string)
						{
							var str = (string) valueObj;
							FeatureSymbol symbol;
							if (_symbols.TryGetValue(str, out symbol))
							{
								if (symbol.Feature != f)
									throw new ArgumentException("A specified symbol is not associated with the specified feature.", "contents");
								v = new SymbolicFeatureValue(symbol);
							}
							else
							{
								if (f == null)
								{
									f = new StringFeature((string) pair.Feature);
									_features.Add(f);
								}
								v = new StringFeatureValue(str);
							}
						}
						else if (valueObj is FeatStruc)
						{
							if (f == null)
							{
								f = new ComplexFeature((string) pair.Feature);
								_features.Add(f);
							}
							var fs = (FeatStruc) valueObj;
							v = GenerateFeatureStructure(fs.Contents);
						}
						else if (valueObj is FeatureValue)
						{
							v = (FeatureValue) valueObj;
							if (f == null)
							{
								switch (v.Type)
								{
									case FeatureValueType.Complex:
										f = new ComplexFeature((string) pair.Feature);
										break;

									case FeatureValueType.String:
										f = new StringFeature((string) pair.Feature);
										break;
								}
								_features.Add(f);
							}
						}
						else
						{
							throw new ArgumentException("An invalid value was specified.");
						}
					}
					else
					{
						var symbols = new List<FeatureSymbol>();
						foreach (object val in pair.Values)
						{
							if (val is FeatureSymbol)
							{
								var symbol = (FeatureSymbol) val;
								if (symbol.Feature != f)
									throw new ArgumentException("A specified symbol is not associated with the specified feature.", "contents");
								symbols.Add(symbol);
							}
							else if (val is string)
							{
								var str = (string) val;
								FeatureSymbol symbol;
								if (_symbols.TryGetValue(str, out symbol))
								{
									if (symbol.Feature != f)
										throw new ArgumentException("A specified symbol is not associated with the specified feature.", "contents");
									symbols.Add(symbol);
								}
								else
								{
									throw new ArgumentException("An invalid value was specified", "contents");
								}
							}
							else
							{
								throw new ArgumentException("An invalid value was specified", "contents");
							}
						}
						v = new SymbolicFeatureValue(symbols);
					}

					if (f == null)
						throw new ArgumentException("An invalid feature was specified", "contents");
					featureValues.Add(f, v);
				}
				else if (obj is string)
				{
					var str = (string) obj;
					FeatureSymbol symbol;
					if (_symbols.TryGetValue(str, out symbol))
						featureValues.Add(symbol.Feature, new SymbolicFeatureValue(symbol));
					else
						throw new ArgumentException("An invalid value was specified", "contents");
				}
				else if (obj is FeatureSymbol)
				{
					var symbol = (FeatureSymbol) obj;
					featureValues.Add(symbol.Feature, new SymbolicFeatureValue(symbol));
				}
				else
				{
					throw new ArgumentException("An invalid feature or value was specified", "contents");
				}
			}

			if (_features.All(f => f.ValueType == FeatureValueType.Symbol) && _symbols.Count <= SymbolicFeatureValueSet.MaxNumValues)
				return new SymbolicFeatureValueSet(featureValues.Values.SelectMany(val => ((SimpleFeatureValue<FeatureSymbol>) val).Values), this);

			var output = new FeatureDictionary(this);
			foreach (KeyValuePair<Feature, FeatureValue> kvp in featureValues)
				output.Add(kvp.Key, kvp.Value);
			return output;
		}

		public FeatureStructure CreateAntiFeatureStructure(FeatureStructure fs)
		{
			return GenerateFeatureStructure(CreateAntiFeatStruc(fs));
		}

		private IEnumerable<object> CreateAntiFeatStruc(FeatureStructure fs)
		{
			return (from feature in fs.Features
				    let value = GetAntiValue(feature, fs.GetValue(feature))
				    where value != null
				    select new FeatValPair(feature, value)).Cast<object>();
		}

		private object GetAntiValue(Feature feature, FeatureValue value)
		{
			switch (feature.ValueType)
			{
				case FeatureValueType.String:
					// TODO: how do we handle strings?
					break;

				case FeatureValueType.Symbol:
					var symbolFeature = (SymbolicFeature)feature;
					var symbolValue = (SymbolicFeatureValue)value;
					return new SymbolicFeatureValue(symbolFeature.PossibleSymbols.Except(symbolValue.Values));

				case FeatureValueType.Complex:
					var complexValue = (FeatureStructure)value;
					return new FeatStruc(CreateAntiFeatStruc(complexValue));
			}
			return null;
		}

		public FeatureStructure CreateAnalysisFeatureStructure(FeatureStructure fs)
		{
			return GenerateFeatureStructure(CreateAnalysisFeatStruc(fs, Features));
		}

		public FeatureStructure CreateAnalysisFeatureStructure()
		{
			return CreateAnalysisFeatureStructure(null);
		}

		private IEnumerable<object> CreateAnalysisFeatStruc(FeatureStructure fs, IEnumerable<Feature> features)
		{
			return (from feature in features
				    let value = GetAnalysisValue(feature, fs == null ? null : fs.GetValue(feature))
				    where value != null
				    select new FeatValPair(feature, value)).Cast<object>();
		}

		private object GetAnalysisValue(Feature feature, FeatureValue value)
		{
			switch (feature.ValueType)
			{
				case FeatureValueType.String:
					// TODO: how do we handle strings?
					break;

				case FeatureValueType.Symbol:
					var symbolFeature = (SymbolicFeature)feature;
					var symbolValue = (SymbolicFeatureValue)value;
					if (symbolValue == null)
						return new SymbolicFeatureValue(symbolFeature.PossibleSymbols);
					return new SymbolicFeatureValue(symbolValue.Values);

				case FeatureValueType.Complex:
					var complexFeature = (ComplexFeature)feature;
					var complexValue = (FeatureStructure)value;
					return new FeatStruc(CreateAnalysisFeatStruc(complexValue, complexFeature.Subfeatures));
			}
			return null;
		}


        /// <summary>
        /// Gets the feature associated with the specified ID.
        /// </summary>
        /// <param name="id">The ID.</param>
        /// <returns>The feature.</returns>
        public Feature GetFeature(string id)
        {
            Feature feature;
            if (_features.TryGetValue(id, out feature))
                return feature;
            return null;
        }

        /// <summary>
        /// Gets the feature value associated with the specified ID.
        /// </summary>
        /// <param name="id">The ID.</param>
        /// <returns>The feature value.</returns>
        public FeatureSymbol GetSymbol(string id)
        {
            FeatureSymbol symbol;
            if (_symbols.TryGetValue(id, out symbol))
                return symbol;
            return null;
        }

        /// <summary>
        /// Adds the feature.
        /// </summary>
        /// <param name="feature">The feature.</param>
        public void AddFeature(Feature feature)
        {
            _features.Add(feature);
        }

        /// <summary>
        /// Adds the feature value.
        /// </summary>
        /// <param name="symbol">The feature value.</param>
		/// <exception cref="System.InvalidOperationException">Thrown when the current number of feature values is equal to the maximum
		/// and this feature system does not contain the specified value.</exception>
        public void AddSymbol(FeatureSymbol symbol)
        {
            symbol.SymbolIndex = _symbols.Count;
            _symbols.Add(symbol);
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        public void Reset()
        {
            _features.Clear();
            _symbols.Clear();
        }
    }
}
