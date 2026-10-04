using Application.Abstraction.Data;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed record SystemPageContent(string Html, string Css, HashSet<int> ExcludedSiteCodeIds);
