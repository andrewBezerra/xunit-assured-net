using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

using Shouldly;

namespace XUnitAssured.Core.Logging;

/// <summary>
/// One entry the system under test logged, as the capture saw it.
/// </summary>
/// <param name="Category">The logger category, usually the full name of the class that logged</param>
/// <param name="Level">The log level</param>
/// <param name="EventId">The event id; <c>Id</c> is 0 when the call did not set one</param>
/// <param name="Message">The formatted message</param>
/// <param name="Exception">The exception logged with the entry, if any</param>
/// <param name="Properties">The structured values of the entry (message template arguments), by name</param>
/// <param name="Scopes">The scopes active when the entry was written, as text</param>
public sealed record CapturedLog(
	string Category,
	LogLevel Level,
	EventId EventId,
	string Message,
	Exception? Exception,
	IReadOnlyDictionary<string, object?> Properties,
	IReadOnlyList<string> Scopes)
{
	/// <summary>
	/// Everything this entry would put on the wire: the message, the exception with its stack
	/// trace, the structured values and the scopes. It is what a "nothing sensitive leaked" check
	/// has to look at, since a value that stays out of the message can still ride in a scope.
	/// </summary>
	public string FullText =>
		string.Join("\n", new[] { Message, Exception?.ToString() }
			.Concat(Properties.Select(p => $"{p.Key}={p.Value}"))
			.Concat(Scopes)
			.Where(t => !string.IsNullOrEmpty(t)));
}

/// <summary>
/// An <see cref="ILoggerProvider"/> that records every entry, for asserting what the system
/// logged — "a 500 leaves exactly one error entry", "refusing access logs a security event",
/// "no log line carries this value".
/// </summary>
/// <remarks>
/// <para>
/// Register it on the host under test, and let everything through so the levels configured for
/// the environment do not hide what the test is about:
/// </para>
/// <code>
/// var logs = new LogCapture();
///
/// new WebApplicationFactory&lt;Program&gt;().WithWebHostBuilder(b =&gt;
///     b.ConfigureLogging(log =&gt;
///     {
///         log.SetMinimumLevel(LogLevel.Trace);
///         log.AddProvider(logs);
///     }));
/// </code>
/// <para>
/// A fixture shared by many tests sees every test's entries: call <see cref="Clear"/> right
/// before the action under test, or filter by something only this test produced.
/// </para>
/// </remarks>
public sealed class LogCapture : ILoggerProvider, ISupportExternalScope
{
	private readonly List<CapturedLog> _entradas = [];
	private readonly object _trava = new();
	private IExternalScopeProvider _escopos = new LoggerExternalScopeProvider();

	/// <summary>Every entry captured so far, in the order it was written.</summary>
	public IReadOnlyList<CapturedLog> Entries
	{
		get { lock (_trava) return [.. _entradas]; }
	}

	/// <summary>Forgets what was captured, so the next assertions see only what follows.</summary>
	public void Clear()
	{
		lock (_trava) _entradas.Clear();
	}

	/// <summary>
	/// The entries whose category starts with <paramref name="categoryPrefix"/> (ordinal), at
	/// <paramref name="minimumLevel"/> or above, that satisfy <paramref name="predicate"/>.
	/// </summary>
	public IReadOnlyList<CapturedLog> Where(
		string? categoryPrefix = null,
		LogLevel minimumLevel = LogLevel.Trace,
		Func<CapturedLog, bool>? predicate = null) =>
		Entries
			.Where(e => categoryPrefix == null || e.Category.StartsWith(categoryPrefix, StringComparison.Ordinal))
			.Where(e => e.Level >= minimumLevel)
			.Where(e => predicate == null || predicate(e))
			.ToList();

	/// <summary>
	/// Asserts that at least one entry matches. The failure lists what was logged in that
	/// category and level, so a near miss is visible.
	/// </summary>
	/// <param name="categoryPrefix">Category prefix, e.g. the full name of the class; null for any</param>
	/// <param name="minimumLevel">Lowest level that counts</param>
	/// <param name="predicate">Extra condition, e.g. on the event id or the message</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The capture, for chaining</returns>
	public LogCapture AssertLogged(
		string? categoryPrefix = null,
		LogLevel minimumLevel = LogLevel.Trace,
		Func<CapturedLog, bool>? predicate = null,
		string? failureMessage = null)
	{
		Where(categoryPrefix, minimumLevel, predicate).ShouldNotBeEmpty(
			$"{failureMessage ?? "Expected a matching log entry"}. {Descrever(categoryPrefix, minimumLevel)}");
		return this;
	}

	/// <summary>
	/// Asserts that exactly one entry matches — "one error opens one alert, not three".
	/// </summary>
	/// <returns>The capture, for chaining</returns>
	public LogCapture AssertLoggedOnce(
		string? categoryPrefix = null,
		LogLevel minimumLevel = LogLevel.Trace,
		Func<CapturedLog, bool>? predicate = null,
		string? failureMessage = null)
	{
		var achadas = Where(categoryPrefix, minimumLevel, predicate);
		achadas.Count.ShouldBe(1,
			$"{failureMessage ?? "Expected exactly one matching log entry"}, but found {achadas.Count}: " +
			string.Join(" | ", achadas.Select(Resumo)));
		return this;
	}

	/// <summary>
	/// Asserts that no entry matches <paramref name="predicate"/> — e.g. that no log line carries
	/// a sensitive value: <c>logs.AssertNotLogged(e =&gt; e.FullText.Contains(cpf))</c>.
	/// </summary>
	/// <returns>The capture, for chaining</returns>
	public LogCapture AssertNotLogged(Func<CapturedLog, bool> predicate, string? failureMessage = null)
	{
		if (predicate == null) throw new ArgumentNullException(nameof(predicate));

		var achadas = Entries.Where(predicate).ToList();
		achadas.ShouldBeEmpty(
			$"{failureMessage ?? "Expected no matching log entry"}, but found {achadas.Count}: " +
			string.Join(" | ", achadas.Select(Resumo)));
		return this;
	}

	/// <summary>
	/// Asserts that no entry — message, exception, structured values or scopes — contains
	/// <paramref name="text"/> (ordinal).
	/// </summary>
	/// <returns>The capture, for chaining</returns>
	public LogCapture AssertNothingContains(string text) =>
		AssertNotLogged(e => e.FullText.Contains(text, StringComparison.Ordinal),
			$"Expected no log entry to contain '{text}'");

	/// <inheritdoc />
	public ILogger CreateLogger(string categoryName) => new Registrador(this, categoryName);

	/// <inheritdoc />
	public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _escopos = scopeProvider;

	/// <inheritdoc />
	public void Dispose() { }

	private void Anotar(CapturedLog entrada)
	{
		lock (_trava) _entradas.Add(entrada);
	}

	private string Descrever(string? categoria, LogLevel nivel)
	{
		var vistas = Where(categoria, nivel);
		return vistas.Count == 0
			? $"Nothing was logged{(categoria == null ? "" : $" in {categoria}*")} at {nivel} or above."
			: $"Logged there: {string.Join(" | ", vistas.Select(Resumo))}";
	}

	private static string Resumo(CapturedLog e) =>
		$"[{e.Level}] {e.Category} ({e.EventId.Id}): {e.Message}";

	private sealed class Registrador(LogCapture dono, string categoria) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
			dono._escopos.Push(state);

		public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
			Exception? exception, Func<TState, Exception?, string> formatter)
		{
			var propriedades = new Dictionary<string, object?>(StringComparer.Ordinal);
			if (state is IEnumerable<KeyValuePair<string, object?>> pares)
			{
				foreach (var par in pares)
					propriedades[par.Key] = par.Value;
			}

			var escopos = new List<string>();
			dono._escopos.ForEachScope((escopo, lista) => lista.Add(escopo?.ToString() ?? string.Empty), escopos);

			dono.Anotar(new CapturedLog(
				categoria,
				logLevel,
				eventId,
				formatter(state, exception),
				exception,
				propriedades,
				escopos));
		}
	}
}
