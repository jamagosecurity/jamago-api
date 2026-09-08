using FluentValidation;

namespace Jama.Application.Drawings.Commands.UpdateDrawing;

public sealed class UpdateDrawingCommandValidator : AbstractValidator<UpdateDrawingCommand>
{
    public UpdateDrawingCommandValidator() => DrawingWriteRules.Apply(this);
}
