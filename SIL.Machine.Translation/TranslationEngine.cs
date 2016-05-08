﻿using System;
using System.Collections.Generic;
using System.Linq;
using SIL.ObjectModel;
using SIL.Progress;

namespace SIL.Machine.Translation
{
	public class TranslationEngine : DisposableBase
	{
		private readonly TransferEngine _transferEngine;
		private readonly ISmtEngine _smtEngine;
		private readonly HashSet<TranslationSession> _sessions;

		public TranslationEngine(ISmtEngine smtEngine, TransferEngine transferEngine = null)
		{
			_smtEngine = smtEngine;
			_transferEngine = transferEngine;
			_sessions = new HashSet<TranslationSession>();
		}

		public IEnumerable<IEnumerable<string>> SourceCorpus { get; set; }
		public IEnumerable<IEnumerable<string>> TargetCorpus { get; set; }

		public void Rebuild(IProgress progress = null)
		{
			lock (_sessions)
			{
				if (_sessions.Count > 0)
					throw new InvalidOperationException("The engine cannot be trained while there are active sessions open.");

				if (SourceCorpus != null && TargetCorpus != null)
					_smtEngine.Train(SourceCorpus, TargetCorpus, progress);
			}
		}

		public void Save()
		{
			_smtEngine.SaveModels();
		}

		public TranslationSession StartSession()
		{
			var session = new TranslationSession(this, _smtEngine.StartSession(), _transferEngine);
			lock (_sessions)
				_sessions.Add(session);
			return session;
		}

		internal void RemoveSession(TranslationSession session)
		{
			lock (_sessions)
				_sessions.Remove(session);
		}

		protected override void DisposeManagedResources()
		{
			lock (_sessions)
			{
				foreach (TranslationSession session in _sessions.ToArray())
					session.Dispose();
			}
		}
	}
}