using Application.Abstraction.Behaviors;
using Application.Abstraction.Security;
using Application.Abstraction.Services.Authentication;
using MediatR;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Security;

/// <summary>
/// The only place authorisation is actually enforced for application requests.
/// Hiding a button is presentation; this is what stops someone who drives the
/// command directly. If it silently stopped working, every permission gate in the
/// CMS would open at once and nothing on screen would look different — which is
/// exactly why it is worth pinning down here.
/// </summary>
public sealed class PermissionPipelineBehaviorTests
{
    private const string Required = "Pages.Publish";

    // ── Requests used as fixtures ─────────────────────────────────────────

    private sealed record GuardedCommand : IRequest<Result>, IRequirePermission
    {
        public static string RequiredPermission => Required;
    }

    private sealed record GuardedQuery : IRequest<Result<int>>, IRequirePermission
    {
        public static string RequiredPermission => Required;
    }

    private sealed record OpenCommand : IRequest<Result>;

    /// <summary>Declares a permission but cannot express a refusal in its response.</summary>
    private sealed record GuardedButUnableToRefuse : IRequest<int>, IRequirePermission
    {
        public static string RequiredPermission => Required;
    }

    private static PermissionPipelineBehavior<TRequest, TResponse> Behavior<TRequest, TResponse>(bool granted)
        where TRequest : class =>
        new(new StubUserContext(granted));

    // ── Refusal ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_command_the_caller_is_not_permitted_to_run_is_refused()
    {
        bool handlerRan = false;

        Result result = await Behavior<GuardedCommand, Result>(granted: false)
            .Handle(new GuardedCommand(), () =>
            {
                handlerRan = true;
                return Task.FromResult(Result.Success());
            }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Permission.NotGranted");

        // The point of a pre-handler gate: the work must not happen at all, rather
        // than happening and having its result discarded.
        handlerRan.ShouldBeFalse();
    }

    [Fact]
    public async Task A_refused_query_comes_back_as_a_failed_result_of_the_right_type()
    {
        // Result<T> is built by reflection here, so a change to Result.Failure's
        // shape would break it in a way nothing else would notice.
        Result<int> result = await Behavior<GuardedQuery, Result<int>>(granted: false)
            .Handle(new GuardedQuery(), () => Task.FromResult(Result.Success(42)), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Permission.NotGranted");
    }

    [Fact]
    public async Task The_refusal_names_the_permission_that_was_missing()
    {
        Result result = await Behavior<GuardedCommand, Result>(granted: false)
            .Handle(new GuardedCommand(), () => Task.FromResult(Result.Success()), CancellationToken.None);

        result.Error.Description.ShouldContain(Required);
    }

    [Fact]
    public async Task A_guarded_request_that_cannot_express_a_refusal_throws_rather_than_running()
    {
        // No Result to fail, so there is no way to report "refused". Failing loudly
        // is the only safe option; running it unauthorised is not.
        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            Behavior<GuardedButUnableToRefuse, int>(granted: false)
                .Handle(new GuardedButUnableToRefuse(), () => Task.FromResult(7), CancellationToken.None));
    }

    // ── Passage ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_caller_holding_the_permission_reaches_the_handler()
    {
        bool handlerRan = false;

        Result result = await Behavior<GuardedCommand, Result>(granted: true)
            .Handle(new GuardedCommand(), () =>
            {
                handlerRan = true;
                return Task.FromResult(Result.Success());
            }, CancellationToken.None);

        handlerRan.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_request_declaring_no_permission_is_never_gated()
    {
        // Opt-in by design: profile reads, sign-in and list screens are open to any
        // signed-in user. If this ever started denying, the CMS would lock everyone
        // out of everything at once.
        bool handlerRan = false;

        Result result = await Behavior<OpenCommand, Result>(granted: false)
            .Handle(new OpenCommand(), () =>
            {
                handlerRan = true;
                return Task.FromResult(Result.Success());
            }, CancellationToken.None);

        handlerRan.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task The_permission_it_checks_is_the_one_the_request_declared()
    {
        // Guards the reflection that reads a static abstract member: were it to
        // return null, every guarded request would silently become open.
        RecordingUserContext recorder = new();

        await new PermissionPipelineBehavior<GuardedCommand, Result>(recorder)
            .Handle(new GuardedCommand(), () => Task.FromResult(Result.Success()), CancellationToken.None);

        recorder.Asked.ShouldBe(Required);
    }

    private sealed class StubUserContext(bool granted) : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => granted;
        public bool CanAuthorCustomCode => granted;
        public bool HasPermission(string permissionKey) => granted;
        public bool IsInRole(string roleName) => granted;
    }

    private sealed class RecordingUserContext : IUserContext
    {
        public string? Asked { get; private set; }

        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => true;
        public bool CanAuthorCustomCode => true;
        public bool IsInRole(string roleName) => true;

        public bool HasPermission(string permissionKey)
        {
            Asked = permissionKey;
            return true;
        }
    }
}
