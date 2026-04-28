namespace LQ.Models.DTOs;

public record SuspiciousListDTO(int FieldIndex, string FieldName, string Separator, List<string> Samples);
public record UploadResultDTO(string FileName, int FileId, List<SuspiciousListDTO> SuspiciousLists);
public record ConfirmFieldDTO(int FieldIndex, string Separator);
public record ConfirmListRequestDTO(int FileId, List<ConfirmFieldDTO> Confirmed);
