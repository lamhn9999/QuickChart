namespace LQ.Models.DTOs;

public record SuspiciousListDTO(int FieldIndex, string FieldName, string Separator, List<string> Samples);
public record UploadResultDTO(string FileName, Guid Token, List<SuspiciousListDTO> SuspiciousLists);
public record ConfirmFieldDTO(int FieldIndex, string Separator);
public record ConfirmListRequestDTO(Guid Token, List<ConfirmFieldDTO> Confirmed);
public record PendingUploadDTO(Guid Token, string FileName, string Status, List<SuspiciousListDTO>? Suspicious, string? ErrorMessage);
