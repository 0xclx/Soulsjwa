namespace Soulsjwa.Api.Features.Legal.Entities;

/// <summary>
/// The two legal documents an operator may publish. Fixed set — new kinds
/// are a schema change, not admin-configurable data.
/// </summary>
public enum LegalDocumentKind
{
    Impressum = 0,
    Datenschutz = 1,
}
