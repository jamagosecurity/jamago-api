using Jama.Application.Common;
using Jama.Application.DTOs;
using Jama.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Jama.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactsController(IContactService contacts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ContactSubmissionDto>>> GetContacts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await contacts.GetPagedAsync(new PaginationQuery { Page = page, PageSize = pageSize }, ct);
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
        return CreatedAtAction(nameof(GetContacts), new { page = 1, pageSize = 20 }, created);
    }
}
