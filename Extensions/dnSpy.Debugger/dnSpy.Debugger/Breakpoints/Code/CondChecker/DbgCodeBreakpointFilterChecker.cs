﻿/*
    Copyright (C) 2014-2017 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using dnSpy.Contracts.Debugger;
using dnSpy.Contracts.Debugger.Breakpoints.Code;
using dnSpy.Contracts.Debugger.Breakpoints.Code.FilterExpressionEvaluator;

namespace dnSpy.Debugger.Breakpoints.Code.CondChecker {
	abstract class DbgCodeBreakpointFilterChecker {
		public abstract bool ShouldBreak(DbgBoundCodeBreakpoint boundBreakpoint, DbgThread thread, DbgCodeBreakpointFilter filter);
	}

	[Export(typeof(DbgCodeBreakpointFilterChecker))]
	sealed class DbgCodeBreakpointFilterCheckerImpl : DbgCodeBreakpointFilterChecker {
		readonly Lazy<DbgFilterExpressionEvaluator, IDbgFilterExpressionEvaluatorMetadata> dbgFilterExpressionEvaluator;
		readonly DbgFilterEEVariableProviderImpl dbgFilterEEVariableProvider;

		[ImportingConstructor]
		DbgCodeBreakpointFilterCheckerImpl([ImportMany] IEnumerable<Lazy<DbgFilterExpressionEvaluator, IDbgFilterExpressionEvaluatorMetadata>> dbgFilterExpressionEvaluators) {
			dbgFilterEEVariableProvider = new DbgFilterEEVariableProviderImpl();
			dbgFilterExpressionEvaluator = dbgFilterExpressionEvaluators.OrderBy(a => a.Metadata.Order).FirstOrDefault();
		}

		sealed class DbgFilterEEVariableProviderImpl : DbgFilterEEVariableProvider {
			public override string MachineName => Environment.MachineName;
			public override int ProcessId => process.Id;
			public override string ProcessName => process.Filename;
			public override int ThreadId => thread?.Id ?? -1;
			public override string ThreadName => thread?.UIName;

			DbgProcess process;
			DbgThread thread;

			public void Initialize(DbgProcess process, DbgThread thread) {
				this.process = process ?? throw new ArgumentNullException(nameof(process));
				this.thread = thread;
			}

			public void Clear() {
				process = null;
				thread = null;
			}
		}

		public override bool ShouldBreak(DbgBoundCodeBreakpoint boundBreakpoint, DbgThread thread, DbgCodeBreakpointFilter filter) {
			// If there's no FEE, assume expression matches everything
			if (dbgFilterExpressionEvaluator == null)
				return true;

			var expr = filter.Filter;
			Debug.Assert(expr != null);
			if (expr == null)
				return false;

			try {
				dbgFilterEEVariableProvider.Initialize(boundBreakpoint.Process, thread);
				var res = dbgFilterExpressionEvaluator.Value.Evaluate(expr, dbgFilterEEVariableProvider);
				if (res.HasError)
					return false;
				return res.Result;
			}
			finally {
				dbgFilterEEVariableProvider.Clear();
			}
		}
	}
}