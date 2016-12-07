﻿using System;
using System.Collections.Generic;
using Bridge.Html5;
using SIL.Machine.Web;

namespace SIL.Machine.Translation
{
	public class TranslationEngine
	{
		public TranslationEngine(string baseUrl, string sourceLanguageTag, string targetLanguageTag)
			: this(baseUrl, sourceLanguageTag, targetLanguageTag, new AjaxWebClient())
		{
		}

		public TranslationEngine(string baseUrl, string sourceLanguageTag, string targetLanguageTag, IWebClient webClient)
		{
			BaseUrl = baseUrl;
			SourceLanguageTag = sourceLanguageTag;
			TargetLanguageTag = targetLanguageTag;
			WebClient = webClient;
			ErrorCorrectingModel = new ErrorCorrectingModel();
			ConfidenceThreshold = 0.2;
		}

		public string SourceLanguageTag { get; }
		public string TargetLanguageTag { get; }
		public string BaseUrl { get; }
		public double ConfidenceThreshold { get; set; }
		internal IWebClient WebClient { get; }
		internal ErrorCorrectingModel ErrorCorrectingModel { get; }

		public void GetSuggester(string[] sourceSegment, Action<InteractiveTranslationSuggester> onFinished)
		{
			string url = string.Format("{0}/translation/engines/{1}/{2}/actions/interactive-translate", BaseUrl, SourceLanguageTag, TargetLanguageTag);
			string body = JSON.Stringify(sourceSegment);
			WebClient.Send("POST", url, body, "application/json", responseText => onFinished(CreateSuggester(sourceSegment, JSON.Parse(responseText))),
				status => onFinished(null));
		}

		private InteractiveTranslationSuggester CreateSuggester(string[] sourceSegment, dynamic json)
		{
			WordGraph wordGraph = ParseWordGraph(json["wordGraph"]);
			TranslationResult ruleResult = ParseRuleResult(sourceSegment, json["ruleResult"]);
			return new InteractiveTranslationSuggester(this, wordGraph, ruleResult, sourceSegment);
		}

		private WordGraph ParseWordGraph(dynamic jsonWordGraph)
		{
			double initialStateScore = jsonWordGraph["initialStateScore"];

			var finalStates = new List<int>();
			var jsonFinalStates = jsonWordGraph["finalStates"];
			foreach (var jsonFinalState in jsonFinalStates)
				finalStates.Add(jsonFinalState);

			var jsonArcs = jsonWordGraph["arcs"];
			var arcs = new List<WordGraphArc>();
			foreach (var jsonArc in jsonArcs)
			{
				int prevState = jsonArc["prevState"];
				int nextState = jsonArc["nextState"];
				double score = jsonArc["score"];

				var jsonWords = jsonArc["words"];
				var words = new List<string>();
				foreach (var jsonWord in jsonWords)
					words.Add(jsonWord);

				var jsonConfidences = jsonArc["confidences"];
				var confidences = new List<double>();
				foreach (var jsonConfidence in jsonConfidences)
					confidences.Add(jsonConfidence);

				int srcStartIndex = jsonArc["sourceStartIndex"];
				int endStartIndex = jsonArc["sourceEndIndex"];
				bool isUnknown = jsonArc["isUnknown"];

				var jsonAlignment = jsonArc["alignment"];
				var alignment = new WordAlignmentMatrix(endStartIndex - srcStartIndex + 1, words.Count);
				foreach (var jsonAligned in jsonAlignment)
				{
					int i = jsonAligned["sourceIndex"];
					int j = jsonAligned["targetIndex"];
					alignment[i, j] = AlignmentType.Aligned;
				}

				arcs.Add(new WordGraphArc(prevState, nextState, score, words.ToArray(), alignment, confidences.ToArray(),
					srcStartIndex, endStartIndex, isUnknown));
			}

			return new WordGraph(arcs, finalStates, initialStateScore);
		}

		private TranslationResult ParseRuleResult(string[] sourceSegment, dynamic jsonResult)
		{
			var jsonTarget = jsonResult["target"];
			var targetSegment = new List<string>();
			foreach (var jsonWord in jsonTarget)
				targetSegment.Add(jsonWord);

			var jsonConfidences = jsonResult["confidences"];
			var confidences = new List<double>();
			foreach (var jsonConfidence in jsonConfidences)
				confidences.Add(jsonConfidence);

			var jsonAlignment = jsonResult["alignment"];
			var alignment = new AlignedWordPair[sourceSegment.Length, targetSegment.Count];
			foreach (var jsonAligned in jsonAlignment)
			{
				int i = jsonAligned["sourceIndex"];
				int j = jsonAligned["targetIndex"];
				var sources = (TranslationSources) jsonAligned["sources"];
				alignment[i, j] = new AlignedWordPair(i, j, sources);
			}

			return new TranslationResult(sourceSegment, targetSegment, confidences, alignment);
		}
	}
}