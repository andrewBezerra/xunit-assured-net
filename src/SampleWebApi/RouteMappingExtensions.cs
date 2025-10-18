using SampleWebApi;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using static SampleWebApi.HandlerTest;

namespace SampleWebApi;

public static class RouteMappingExtensions
{
	public static WebApplication MapTestApplication(this WebApplication app) =>
		app.MapPost();

	private static WebApplication MapPost(this WebApplication app)
	{
		app.MapPost("v1/test", async (Request request, [FromServices] IMediator mediator) =>
		{
			var result = await mediator.Send(request);

			return result switch
			{
				BadRequestResponse bad => Results.Problem(statusCode: 400, title: bad.Title, detail: bad.Detail),
				GoneResponse gone => Results.Problem(statusCode: 410, title: gone.Title, detail: gone.Detail),
				UnprocessableEntityResponse unprocessable => Results.Problem(statusCode: 422, title: unprocessable.Title, detail: unprocessable.Detail),
				FailResponse fail => Results.Problem(statusCode: 500, title: fail.Title, detail: fail.Detail),
				SuccessResponse success => Results.Ok(success),
				_ => Results.Problem("Erro desconhecido."),
			};
		})
		.WithOpenApi()
		.Produces(200, typeof(SuccessResponse))
		.ProducesValidationProblem()
		.ProducesProblem(410)
		.ProducesProblem(422)
		.ProducesProblem(500);
		return app;
	}

	public record ResponseError(string nome, string endereco);

}
