using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using XUnitAssured.Core.Logging;

namespace XUnitAssured.Http.AspNetCore;

/// <summary>
/// A fábrica de logs da API, envolvida: cada logger escreve no de origem, como sempre, e também
/// na captura, em todos os níveis.
/// </summary>
/// <remarks>
/// Envolver a fábrica, e não registrar a captura como provedor, é o que a faz valer para qualquer
/// biblioteca de log. O <c>UseSerilog</c> troca o <see cref="ILoggerFactory"/> e ignora os
/// provedores registrados; e, com provedor, o nível mínimo do ambiente cortaria o que o teste quer
/// ver antes de chegar à captura.
/// </remarks>
internal sealed class FabricaQueCaptura(ILoggerFactory origem, LogCapture captura, bool descartaOrigem) : ILoggerFactory
{
	/// <summary>Troca o registro do <see cref="ILoggerFactory"/> por um que envolve o de origem.</summary>
	public static void Envolver(IServiceCollection servicos, LogCapture captura)
	{
		var registro = servicos.LastOrDefault(s => s.ServiceType == typeof(ILoggerFactory) && !s.IsKeyedService);
		if (registro == null)
		{
			servicos.AddLogging();
			registro = servicos.Last(s => s.ServiceType == typeof(ILoggerFactory) && !s.IsKeyedService);
		}

		servicos.Remove(registro);
		servicos.AddSingleton<ILoggerFactory>(provedor => registro switch
		{
			{ ImplementationInstance: ILoggerFactory instancia } => new FabricaQueCaptura(instancia, captura, descartaOrigem: false),
			{ ImplementationFactory: { } criar } => new FabricaQueCaptura((ILoggerFactory)criar(provedor), captura, descartaOrigem: true),
			_ => new FabricaQueCaptura(
				(ILoggerFactory)ActivatorUtilities.CreateInstance(provedor, registro.ImplementationType!),
				captura, descartaOrigem: true)
		});
	}

	public ILogger CreateLogger(string categoryName) =>
		new LoggerQueCaptura(origem.CreateLogger(categoryName), captura.CreateLogger(categoryName));

	public void AddProvider(ILoggerProvider provider) => origem.AddProvider(provider);

	public void Dispose()
	{
		if (descartaOrigem)
			origem.Dispose();
	}

	private sealed class LoggerQueCaptura(ILogger origem, ILogger captura) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull
		{
			var deOrigem = origem.BeginScope(state);
			var daCaptura = captura.BeginScope(state);
			return new Escopos(deOrigem, daCaptura);
		}

		// A captura quer tudo; a origem decide por si a cada Log.
		public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (origem.IsEnabled(logLevel))
				origem.Log(logLevel, eventId, state, exception, formatter);
			captura.Log(logLevel, eventId, state, exception, formatter);
		}
	}

	private sealed class Escopos(IDisposable? primeiro, IDisposable? segundo) : IDisposable
	{
		public void Dispose()
		{
			segundo?.Dispose();
			primeiro?.Dispose();
		}
	}
}
