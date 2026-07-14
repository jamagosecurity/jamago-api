using Jama.Application.DTOs;
using Jama.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Jama.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactsController(IContactService contacts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContactSubmissionDto>>> GetContacts(CancellationToken ct = default)
    {
        var result = await contacts.GetAllAsync(ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ContactSubmissionDto>> CreateContact(
        [FromBody] CreateContactSubmissionRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Service) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest("FullName, Email, Service, and Message are required.");
        }

        var created = await contacts.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetContacts), created);
    }
}
