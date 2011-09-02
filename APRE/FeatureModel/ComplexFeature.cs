using System;
using System.Collections.Generic;

namespace SIL.APRE.FeatureModel
{
	public class ComplexFeature : Feature
	{
		private readonly IDBearerSet<Feature> _subfeatures;

		public ComplexFeature(string id, string description)
			: base(id, description)
		{
			_subfeatures = new IDBearerSet<Feature>();
		}

		public ComplexFeature(string id)
			: this(id, id)
		{
		}

		public IEnumerable<Feature> Subfeatures
		{
			get { return _subfeatures; }
		}

		public void AddSubfeature(Feature feature)
		{
			_subfeatures.Add(feature);
		}

		public void RemoveSubfeature(Feature feature)
		{
			_subfeatures.Remove(feature);
		}

		public Feature GetSubfeature(string id)
		{
			try
			{
				return _subfeatures[id];
			}
			catch (KeyNotFoundException ex)
			{
				throw new ArgumentException("The specified feature could not be found.", "id", ex);
			}
		}

		public bool TryGetSubfeature(string id, out Feature subfeature)
		{
			return _subfeatures.TryGetValue(id, out subfeature);
		}
	}
}
