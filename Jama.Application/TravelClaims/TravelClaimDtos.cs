namespace Jama.Application.TravelClaims;

public sealed record TravelClaimLineDto(
    Guid Id, string Kind, decimal BillAmount, decimal Amount, int Persons, decimal? RatePerPerson, bool ReadFromReceipt, string FileName);

public sealed record TravelClaimPersonDto(Guid PersonId, string Name, bool AteLunch);

public sealed record TravelClaimEventDto(string Action, string ActorName, DateTime At, string? Note, decimal? Amount);

public sealed record TravelClaimDto(
    Guid Id, string ClaimNumber, Guid ClaimantId, string ClaimantName,
    DateOnly TripOn, TimeOnly? TripTime, string TripType, string? SiteName, string? Pickup, string? Dropoff,
    bool ClaimantAteLunch,
    /// <summary>What was claimed.</summary>
    decimal Total,
    /// <summary>What the accountant approved (equal to or less than claimed); null until approved.</summary>
    decimal? ApprovedAmount, string? AdjustmentReason,
    /// <summary>What is owed / was paid: the approved amount once there is one, otherwise what was claimed.</summary>
    decimal Payable,
    string? Note, string Status,
    DateTime SubmittedAt, DateTime? ApprovedAt, string? ApprovedByName,
    DateTime? RejectedAt, string? RejectedByName, string? RejectReason, DateTime? WithdrawnAt,
    DateTime? PaidAt, string? PaidByName, string? PaidMethod, string? ReimbursementNumber,
    /// <summary>Days from submitted to paid back (or rejected/withdrawn); while still open, days so far.</summary>
    int DaysWaiting,
    IReadOnlyList<TravelClaimLineDto> Lines, IReadOnlyList<TravelClaimPersonDto> People, IReadOnlyList<TravelClaimEventDto> Events);

/// <summary>One person's money: waiting for approval, approved and owed, and what was paid back.</summary>
public sealed record TravelBalanceDto(
    Guid PersonId, string Name, decimal WaitingApproval, int WaitingCount, decimal Owed, int OwedCount,
    decimal PaidThisMonth, decimal PaidTotal, DateTime? LastPaidAt, int OldestOpenDays);

public sealed record ReimbursementDto(
    Guid Id, string Number, Guid PayeeId, string PayeeName, decimal Amount, string Method, string? Reference,
    DateTime PaidAt, string PaidByName, IReadOnlyList<string> ClaimNumbers);

public sealed record MyTravelClaimsDto(TravelBalanceDto Balance, decimal LunchRate, IReadOnlyList<TravelClaimDto> Claims);

public sealed record TravelClaimsBoardDto(
    decimal LunchRate, decimal ToApproveTotal, int ToApproveCount, decimal OwedTotal, int OwedCount, decimal PaidThisMonth,
    IReadOnlyList<TravelBalanceDto> People, IReadOnlyList<TravelClaimDto> Claims, IReadOnlyList<ReimbursementDto> Reimbursements);

public sealed record TravelPersonOption(Guid Id, string Name);

public sealed record LunchRateDto(decimal AmountPerPerson, DateOnly EffectiveFrom, string SetByName, DateTime SetAt);

public sealed record LunchRatesDto(decimal Current, IReadOnlyList<LunchRateDto> History);

public sealed record TravelClaimFile(Stream Content, string FileName, string ContentType);
