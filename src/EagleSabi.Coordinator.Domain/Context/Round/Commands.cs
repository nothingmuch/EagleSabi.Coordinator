using System.Collections.Immutable;
using EagleSabi.Coordinator.Domain.Context.Round.Records;
using EagleSabi.Infrastructure.Common.Abstractions.EventSourcing.Models;
using NBitcoin;
using WalletWasabi.Crypto;

namespace EagleSabi.Coordinator.Domain.Context.Round;

public record StartRoundCommand(RoundParameters RoundParameters, ImmutableSortedSet<OutPoint>? AllowedInputs, Guid IdempotenceId) : ICommand;
public record RegisterInputCommand(Coin Coin, OwnershipProof OwnershipProof, Guid AliceSecret) : ICommand
{
    public Guid IdempotenceId => ComputeHash();
}

public record ConfirmInputConnectionCommand(Coin Coin, OwnershipProof OwnershipProof, Guid IdempotenceId) : ICommand;
public record RemoveInputCommand(OutPoint AliceOutPoint, Guid IdempotenceId) : ICommand;

public record RegisterOutputCommand(Script Script, long Value, Guid IdempotenceId) : ICommand;

public record StartOutputRegistrationCommand(Guid IdempotenceId) : ICommand;

public record StartConnectionConfirmationCommand(Guid IdempotenceId) : ICommand;

public record StartTransactionSigningCommand(Guid IdempotenceId) : ICommand;

public record SucceedRoundCommand(Guid IdempotenceId) : ICommand;

public record NotifyInputReadyToSignCommand(OutPoint AliceOutPoint, Guid IdempotenceId) : ICommand;

public record AddSignatureCommand(OutPoint AliceOutPoint, WitScript WitScript, Guid IdempotenceId) : ICommand;

public record EndRoundCommand(Guid IdempotenceId) : ICommand;