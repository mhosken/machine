﻿using System.Collections.Generic;

namespace SIL.Machine.Translation
{
	public class SymmetrizationSegmentAligner : ISegmentAligner
	{
		private readonly ISegmentAligner _srcTrgAligner;
		private readonly ISegmentAligner _trgSrcAligner;

		public SymmetrizationSegmentAligner(ISegmentAligner srcTrgAligner, ISegmentAligner trgSrcAligner)
		{
			_srcTrgAligner = srcTrgAligner;
			_trgSrcAligner = trgSrcAligner;
		}

		public WordAlignmentMatrix GetBestAlignment(IReadOnlyList<string> sourceSegment, IReadOnlyList<string> targetSegment)
		{
			WordAlignmentMatrix matrix = _srcTrgAligner.GetBestAlignment(sourceSegment, targetSegment);
			WordAlignmentMatrix invMatrix = _trgSrcAligner.GetBestAlignment(targetSegment, sourceSegment);

			invMatrix.Transpose();
			matrix.SymmetrizeWith(invMatrix);
			return matrix;
		}

		public double GetTranslationProbability(string sourceWord, string targetWord)
		{
			double prob = _srcTrgAligner.GetTranslationProbability(sourceWord, targetWord);
			double invProb = _trgSrcAligner.GetTranslationProbability(targetWord, sourceWord);
			return (prob + invProb) / 2;
		}
	}
}