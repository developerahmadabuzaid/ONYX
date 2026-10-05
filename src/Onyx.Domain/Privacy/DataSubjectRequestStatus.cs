namespace Onyx.Domain.Privacy;

/// <summary>Received → Verified → Completed, or Rejected. Only a verified request can be carried out.</summary>
public enum DataSubjectRequestStatus
{
    Received = 0,
    Verified = 1,
    Completed = 2,
    Rejected = 3,
}
