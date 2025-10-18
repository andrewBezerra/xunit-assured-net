using MediatR;

using static SampleWebApi.HandlerTest;

namespace SampleWebApi;

public class HandlerTest : IRequestHandler<Request, Response>
{
	public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
	{
		return await Task.Run(() => new SuccessResponse("VALOR PADRAO", new SuccessItem[] { new SuccessItem("a", 2) }));
	}

	public record Request(string data) : IRequest<Response>;
	public record Response();
	public record SuccessResponse(string data, SuccessItem[] items) : Response;
	public record BadRequestResponse(string Title, string Detail) : Response;
	public record FailResponse(string Title, string Detail) : Response;
	public record UnprocessableEntityResponse(string Title, string Detail) : Response;
	public record GoneResponse(string Title, string Detail) : Response;

	public record SuccessItem(string Name, int Quantity);



}
