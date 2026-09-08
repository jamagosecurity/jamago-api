using FluentValidation;

namespace Jama.Application.Drawings.Commands.CreateDrawing;

public sealed class CreateDrawingCommandValidator : AbstractValidator<CreateDrawingCommand>
{
    public CreateDrawingCommandValidator() => DrawingWriteRules.Apply(this);
}
