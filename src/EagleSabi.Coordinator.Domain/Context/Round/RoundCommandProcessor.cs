using System.Collections.Immutable;
using EagleSabi.Coordinator.Domain.Context.Round.Enums;
using EagleSabi.Coordinator.Domain.Context.Round.Records;
using EagleSabi.Infrastructure.Common.Abstractions.EventSourcing.Dependencies;
using EagleSabi.Infrastructure.Common.Abstractions.EventSourcing.Models;
using EagleSabi.Infrastructure.Common.Records.EventSourcing;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.Models;

namespace EagleSabi.Coordinator.Domain.Context.Round;

public class RoundCommandProcessor : ICommandProcessor
{
    public Result Process(StartRoundCommand command, RoundState state)
    {
        var errors = PrepareErrors();
        if (!IsStateValid(PhaseEnum.New, state, command.GetType().Name, out var errorResult))
            return errorResult;

        return errors.Count > 0 ?
            Result.Fail(errors) :
            Result.Succeed(
                new IEvent[] {
                    new RoundStartedEvent(command.RoundParameters),
                    command.AllowedInputs is null
                        ? new AllInputsAllowedEvent()
                        : new SpecificInputsAllowedEvent(command.AllowedInputs)
                });
    }

    public Result Process(RegisterInputCommand command, RoundState state)
    {
        var errors = PrepareErrors();
        if (!IsStateValid(PhaseEnum.InputRegistration, state, command.GetType().Name, out var errorResult))
            return errorResult; // TODO throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase); in CoordinatorService

        // TODO if (round.IsInputRegistrationEnded(Config.MaxInputCountByRound))

        if (!state.IsInputAllowed(command.Coin.Outpoint))
        {
            // FIXME append to errors after making WabiSabiProtocolException implement IError if that's allowed
            throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputNotWhitelisted);
        }

        if (state.MultipartyTransactionState is ConstructionState txState)
        {
            // Compute but don't commit updated CoinJoin to round state, it will
            // be re-calculated on input confirmation. This is computed it here
            // for validation purposes

            // FIXME append to errors instead of throwing
            _ = txState.AddInput(command.Coin);
        }

        // FIXME commit to coordinator address
        var coinJoinInputCommitmentData = new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", state.Id);

        if (!OwnershipProof.VerifyCoinJoinInputProof(command.OwnershipProof, command.Coin.TxOut.ScriptPubKey, coinJoinInputCommitmentData))
        {
            // FIXME append to errors instead of throwing
            throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongOwnershipProof);
        }

        // FIXME generate an event that says that the input is registered
        // var alice = new Alice(command.Coin, command.OwnershipProof, round, command.AliceSecret);

        // TODO verify that the following checks are redundant and eliminate the corresponding error codes
        //
        // MultipartyTransactionParameters fully determines the allowed values
        // for inputs & outputs independently, and also handles the dust
        // threshold because it enforces standardness.
        //
        // The range proof width should correspond with the allowed value
        // range, taking into account the number of credentials, but these
        // checks represent a prior understanding where multiple inputs were
        // allowed per alice and where there was only one allowable range that
        // applied to the range proofs, input values and output values even
        // though those are now separate.
        //
        // if (alice.TotalInputAmount < round.MinAmountCredentialValue)
        // {
        //     throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
        // }
        // if (alice.TotalInputAmount > round.MaxAmountCredentialValue)
        // {
        //     throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
        // }

        // if (alice.TotalInputVsize > round.MaxVsizeAllocationPerAlice)
        // {
        //     throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchVsize);
        // }

        var amountCredentialTask = round.AmountCredentialIssuer.HandleRequestAsync(request.ZeroAmountCredentialRequests, cancellationToken);
        var vsizeCredentialTask = round.VsizeCredentialIssuer.HandleRequestAsync(request.ZeroVsizeCredentialRequests, cancellationToken);

        if (round.RemainingInputVsizeAllocation < round.MaxVsizeAllocationPerAlice)
        {
            throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.VsizeQuotaExceeded);
        }

        var commitAmountCredentialResponse = await amountCredentialTask.ConfigureAwait(false);
        var commitVsizeCredentialResponse = await vsizeCredentialTask.ConfigureAwait(false);

        alice.SetDeadlineRelativeTo(round.ConnectionConfirmationTimeFrame.Duration);
        round.Alices.Add(alice);

        return errors.Count > 0 ?
            Result.Fail(errors) :
            Result.Succeed(
                new[] { new InputRegisteredEvent(command.AliceSecret, command.Coin, command.OwnershipProof) });
    }

    public Result Process(EndRoundCommand command, RoundState state)
    {
        return Result.Succeed(new RoundEndedEvent());
    }

    public Result Process(ConfirmInputConnectionCommand command, RoundState state)
    {
        return Result.Succeed(new InputConnectionConfirmedEvent(command.Coin, command.OwnershipProof));
    }

    public Result Process(RemoveInputCommand command, RoundState state)
    {
        return Result.Succeed(new InputUnregistered(command.AliceOutPoint));
    }

    public Result Process(RegisterOutputCommand command, RoundState state)
    {
        return Result.Succeed(new OutputRegisteredEvent(command.Script, command.Value));
    }

    public Result Process(StartOutputRegistrationCommand command, RoundState state)
    {
        return Result.Succeed(new OutputRegistrationStartedEvent());
    }

    public Result Process(StartConnectionConfirmationCommand command, RoundState state)
    {
        return Result.Succeed(new InputsConnectionConfirmationStartedEvent());
    }

    public Result Process(StartTransactionSigningCommand command, RoundState state)
    {
        return Result.Succeed(new SigningStartedEvent());
    }

    public Result Process(SucceedRoundCommand command, RoundState state)
    {
        return Result.Succeed(new IEvent[] { new RoundSucceedEvent(), new RoundEndedEvent() });
    }

    public Result Process(NotifyInputReadyToSignCommand command, RoundState state)
    {
        return Result.Succeed(new InputReadyToSignEvent(command.AliceOutPoint));
    }

    public Result Process(AddSignatureCommand command, RoundState state)
    {
        return Result.Succeed(new SignatureAddedEvent(command.AliceOutPoint, command.WitScript));
    }

    public Result Process(ICommand command, IState state)
    {
        if (state is not RoundState roundState)
            throw new ArgumentException($"State should be type of {nameof(RoundState)}.", nameof(state));
        return ProcessDynamic(command, roundState);
    }

    protected Result ProcessDynamic(dynamic command, RoundState state)
    {
        return Process(command, state);
    }

    private static ImmutableArray<IError>.Builder PrepareErrors()
    {
        return ImmutableArray.CreateBuilder<IError>();
    }

    private bool IsStateValid(PhaseEnum expected, RoundState state, string commandName, out Result errorResult)
    {
        var isStateValid = expected == state.Phase;
        errorResult = null!;
        if (!isStateValid)
        {
            errorResult = Result.Fail(
                new Error(
                    $"Unexpected State for '{commandName}'. expected: '{expected}', actual: '{state.Phase}'"));
        }
        return isStateValid;
    }
}