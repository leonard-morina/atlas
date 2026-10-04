namespace Atlas.Accounts.Infrastructure.CoreBanking;

/// <summary>Bound from "Accounts:CoreBanking".</summary>
public sealed class CoreBankingOptions
{
    public const string Section = "Accounts:CoreBanking";

    /// <summary>
    /// The ACCOUNTS service's root; each market's endpoint is "{market}/accounts" under it. The stand-in locally
    /// (http://stubs/corebanking/), core banking across the site-to-site VPN in production (CBS §1).
    /// </summary>
    public required Uri BaseUrl { get; init; }
}
