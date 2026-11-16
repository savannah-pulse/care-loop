"""Provider-level failures exposed by the AI service."""


class AIProviderUnavailableError(RuntimeError):
    """Raised when inference is disabled or not configured."""


class AIProviderCallError(RuntimeError):
    """Raised when the configured model endpoint cannot produce valid output."""
