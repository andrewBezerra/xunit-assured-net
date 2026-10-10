using System;
using System.Collections.Generic;
using System.Linq;

using XUnitAssured.Core.Results;

namespace XUnitAssured.Http.Results;

/// <summary>
/// The responses of a request sent several times at once, with <c>Concurrently</c>, in the order
/// the requests were written.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HttpStepResult.StatusCode"/> is the code every response had, or 0 when they
/// differ; <see cref="TestStepResult.Success"/> is true only when every response succeeded. There
/// is no single body, header or request: read them per response, in <see cref="Responses"/>, or
/// assert on each with <c>AssertEach</c>.
/// </para>
/// </remarks>
public sealed class ConcurrentHttpStepResult : HttpStepResult
{
	/// <summary>Every response, in the order the requests were written.</summary>
	public IReadOnlyList<HttpStepResult> Responses { get; private init; } = Array.Empty<HttpStepResult>();

	/// <summary>The status code of each response, in order — what a failure message should show.</summary>
	public IReadOnlyList<int> StatusCodes => Responses.Select(r => r.StatusCode).ToList();

	internal static ConcurrentHttpStepResult De(IReadOnlyList<HttpStepResult> respostas, DateTimeOffset inicio)
	{
		var codigos = respostas.Select(r => r.StatusCode).Distinct().ToList();
		var todasDeram = respostas.All(r => r.Success);

		return new ConcurrentHttpStepResult
		{
			Responses = respostas,
			Metadata = new StepMetadata
			{
				StartedAt = inicio,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = respostas.All(r => r.Metadata.Status == StepStatus.Succeeded) ? StepStatus.Succeeded : StepStatus.Failed
			},
			Success = todasDeram,
			Errors = respostas
				.SelectMany((r, i) => r.Errors.Select(e => $"Request {i + 1}: {e}"))
				.ToList(),
			Properties = new Dictionary<string, object?>
			{
				["StatusCode"] = codigos.Count == 1 ? codigos[0] : 0
			}
		};
	}
}
