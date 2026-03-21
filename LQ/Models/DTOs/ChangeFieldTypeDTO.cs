namespace  LQ.Models.DTOs;
public record ChangeFieldTypeDTO (
    int FieldId, 
    Type NewType, 
    char? ListSplitChar
);