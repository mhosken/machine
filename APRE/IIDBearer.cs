﻿using System;

namespace SIL.APRE
{
	public interface IIDBearer : IComparable<IIDBearer>, IComparable, IEquatable<IIDBearer>
	{
		string ID { get; }
		string Description { get; }
	}
}
