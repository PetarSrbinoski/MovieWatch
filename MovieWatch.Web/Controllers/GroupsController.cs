using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWatch.Web.Mapper;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Controllers;

[ApiController, Authorize, Route("api/groups")]
public sealed class GroupsController(GroupMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GroupResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await mapper.ListAsync(cancellationToken));
    }

    [HttpGet("{groupId:guid}")]
    public async Task<ActionResult<GroupResponse>> Get(Guid groupId, CancellationToken cancellationToken)
    {
        return Ok(await mapper.GetAsync(groupId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<GroupResponse>> Create(GroupRequest request, CancellationToken cancellationToken)
    {
        var group = await mapper.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { groupId = group.Id }, group);
    }

    [HttpPut("{groupId:guid}")]
    public async Task<ActionResult<GroupResponse>> Update(Guid groupId, GroupRequest request, CancellationToken cancellationToken)
    {
        return Ok(await mapper.UpdateAsync(groupId, request, cancellationToken));
    }

    [HttpDelete("{groupId:guid}")]
    public async Task<IActionResult> Delete(Guid groupId, CancellationToken cancellationToken)
    {
        await mapper.DeleteAsync(groupId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{groupId:guid}/members")]
    public async Task<ActionResult<MembershipResponse>> AddMember(Guid groupId, AddMemberRequest request, CancellationToken cancellationToken)
    {
        var member = await mapper.AddMemberAsync(groupId, request, cancellationToken);
        return CreatedAtAction(nameof(GetMember), new { groupId, membershipId = member.Id }, member);
    }

    [HttpGet("{groupId:guid}/members/{membershipId:guid}")]
    public async Task<ActionResult<MembershipResponse>> GetMember(Guid groupId, Guid membershipId, CancellationToken cancellationToken)
    {
        return Ok(await mapper.GetMemberAsync(groupId, membershipId, cancellationToken));
    }

    [HttpPut("{groupId:guid}/members/{membershipId:guid}")]
    public async Task<ActionResult<MembershipResponse>> SetParticipation(Guid groupId, Guid membershipId,
        ParticipationRequest request, CancellationToken cancellationToken)
    {
        return Ok(await mapper.SetParticipationAsync(groupId, membershipId, request, cancellationToken));
    }

    [HttpDelete("{groupId:guid}/members/{membershipId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid groupId, Guid membershipId, CancellationToken cancellationToken)
    {
        await mapper.RemoveMemberAsync(groupId, membershipId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{groupId:guid}/owner")]
    public async Task<ActionResult<GroupResponse>> TransferOwnership(Guid groupId, TransferOwnershipRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await mapper.TransferOwnershipAsync(groupId, request, cancellationToken));
    }
}
