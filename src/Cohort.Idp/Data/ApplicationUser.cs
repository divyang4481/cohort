using Microsoft.AspNetCore.Identity;

namespace Cohort.Idp.Data;

public class ApplicationUser : IdentityUser
{
	public string FirstName { get; set; } = string.Empty;

	public string LastName { get; set; } = string.Empty;

	public string EmpId { get; set; } = string.Empty;
}
