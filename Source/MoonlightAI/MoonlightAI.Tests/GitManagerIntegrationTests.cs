using Microsoft.Extensions.Logging;
using MoonlightAI.Core.Configuration;
using MoonlightAI.Core.Git;
using MoonlightAI.Core.Models;

namespace MoonlightAI.Tests;

/// <summary>
/// Integration tests for GitManager.
/// These tests require a valid GitHub PAT and network connectivity.
/// Some tests are marked with Skip attributes to prevent accidental modification of repositories.
/// </summary>
public class GitManagerIntegrationTests : IDisposable
{
    private readonly GitManager _gitManager;
    private readonly GitHubConfiguration _config;
    private readonly string _testWorkingDirectory;

    public GitManagerIntegrationTests()
    {
        _testWorkingDirectory = Path.Combine(Path.GetTempPath(), "moonlight-test", Guid.NewGuid().ToString());

        _config = new GitHubConfiguration
        {
            PersonalAccessToken = Environment.GetEnvironmentVariable("GITHUB_PAT") ?? "test-token",
            DefaultBranch = "main",
            WorkingDirectory = _testWorkingDirectory,
            UserName = "MoonlightAI-Test",
            UserEmail = "moonlight-test@example.com"
        };

        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<GitManager>();

        _gitManager = new GitManager(_config, logger);
    }

    [Fact(Skip = "Requires valid GitHub PAT in GITHUB_PAT environment variable")]
    public async Task CloneOrPullAsync_NewRepository_ClonesSuccessfully()
    {
        // Arrange
        var repo = new RepositoryConfiguration
        {
            RepositoryUrl = "https://github.com/SolutionFamily/solution-family"
        };

        // Act
        var localPath = await _gitManager.CloneOrPullAsync(repo);

        // Assert
        Assert.NotNull(localPath);
        Assert.True(Directory.Exists(localPath));
        Assert.True(Directory.Exists(Path.Combine(localPath, ".git")));
        Assert.Equal("SolutionFamily", repo.Owner);
        Assert.Equal("solution-family", repo.Name);
    }

    [Fact(Skip = "Requires valid GitHub PAT in GITHUB_PAT environment variable")]
    public async Task CloneOrPullAsync_ExistingRepository_PullsSuccessfully()
    {
        // Arrange
        var repo = new RepositoryConfiguration
        {
            RepositoryUrl = "https://github.com/SolutionFamily/solution-family"
        };

        // Clone first time
        await _gitManager.CloneOrPullAsync(repo);

        // Act - Clone again (should pull)
        var localPath = await _gitManager.CloneOrPullAsync(repo);

        // Assert
        Assert.NotNull(localPath);
        Assert.True(Directory.Exists(localPath));
    }

    [Fact(Skip = "Requires valid GitHub PAT in GITHUB_PAT environment variable")]
    public async Task GetExistingPullRequestsAsync_ValidRepository_ReturnsOpenPRs()
    {
        // Arrange
        var repo = new RepositoryConfiguration
        {
            RepositoryUrl = "https://github.com/SolutionFamily/solution-family"
        };

        // Act
        var prs = await _gitManager.GetExistingPullRequestsAsync(repo);

        // Assert
        Assert.NotNull(prs);
        // Note: The actual count depends on the repository state
    }

    [Fact]
    public void RepositoryConfiguration_ParsesOwnerAndName_Correctly()
    {
        // Arrange & Act
        var repo1 = new RepositoryConfiguration
        {
            RepositoryUrl = "https://github.com/SolutionFamily/solution-family"
        };

        var repo2 = new RepositoryConfiguration
        {
            RepositoryUrl = "https://github.com/owner/repo.git"
        };

        // Assert
        Assert.Equal("SolutionFamily", repo1.Owner);
        Assert.Equal("solution-family", repo1.Name);

        Assert.Equal("owner", repo2.Owner);
        Assert.Equal("repo", repo2.Name); // .git should be removed
    }

    [Fact(Skip = "Modifies repository - manual test only")]
    public async Task CreateBranchAsync_NewBranch_CreatesSuccessfully()
    {
        // Arrange
        var repo = new RepositoryConfiguration
        {
            RepositoryUrl = "https://github.com/SolutionFamily/solution-family"
        };
        var localPath = await _gitManager.CloneOrPullAsync(repo);
        var branchName = $"test/moonlight-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        // Act
        var result = await _gitManager.CreateBranchAsync(localPath, branchName);

        // Assert
        Assert.True(result);
    }

    [Fact(Skip = "Modifies repository - manual test only")]
    public async Task CommitAndPush_WithChanges_SucceedsEndToEnd()
    {
        // Arrange
        var repo = new RepositoryConfiguration
        {
            RepositoryUrl = "https://github.com/SolutionFamily/solution-family"
        };
        var localPath = await _gitManager.CloneOrPullAsync(repo);
        var branchName = $"test/moonlight-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        await _gitManager.CreateBranchAsync(localPath, branchName);

        // Create a test file
        var testFile = Path.Combine(localPath, "test-file.txt");
        await File.WriteAllTextAsync(testFile, $"Test content {DateTime.UtcNow}");

        // Act
        await _gitManager.CommitChangesAsync(localPath, "Test commit from MoonlightAI");
        await _gitManager.PushBranchAsync(localPath, branchName);

        // Assert - if we get here without exceptions, it worked
        Assert.True(true);

        // Cleanup
        File.Delete(testFile);
    }

    [Fact(Skip = "Creates PR - manual test only")]
    public async Task CreatePullRequestAsync_ValidBranch_CreatesPR()
    {
        // Arrange
        var repo = new RepositoryConfiguration
        {
            RepositoryUrl = "https://github.com/SolutionFamily/solution-family"
        };
        var branchName = $"test/moonlight-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        // Act
        var prUrl = await _gitManager.CreatePullRequestAsync(
            repo,
            branchName,
            "Test PR from MoonlightAI",
            "This is an automated test PR. Please close without merging.");

        // Assert
        Assert.NotNull(prUrl);
        Assert.Contains("github.com", prUrl);
        Assert.Contains("pull", prUrl);
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testWorkingDirectory))
        {
            try
            {
                Directory.Delete(_testWorkingDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
