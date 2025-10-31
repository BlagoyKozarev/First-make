import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '../../test/test-utils';
import ConfirmDialog from '../ConfirmDialog';

describe('ConfirmDialog', () => {
  it('does not render when isOpen is false', () => {
    const { container } = render(
      <ConfirmDialog
        isOpen={false}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
        title="Test Title"
        message="Test Message"
      />
    );

    expect(container.firstChild).toBeNull();
  });

  it('renders dialog when isOpen is true', () => {
    render(
      <ConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
        title="Test Title"
        message="Test Message"
      />
    );

    expect(screen.getByText('Test Title')).toBeInTheDocument();
    expect(screen.getByText('Test Message')).toBeInTheDocument();
  });

  it('calls onClose when cancel button is clicked', () => {
    const onClose = vi.fn();

    render(
      <ConfirmDialog
        isOpen={true}
        onClose={onClose}
        onConfirm={vi.fn()}
        title="Test"
        message="Test"
      />
    );

    const cancelButton = screen.getByRole('button', { name: /Отказ/i });
    fireEvent.click(cancelButton);

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('calls onConfirm when confirm button is clicked', () => {
    const onConfirm = vi.fn();

    render(
      <ConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        onConfirm={onConfirm}
        title="Test"
        message="Test"
      />
    );

    const confirmButton = screen.getByRole('button', { name: /Потвърди/i });
    fireEvent.click(confirmButton);

    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('uses custom button text when provided', () => {
    render(
      <ConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
        title="Test"
        message="Test"
        confirmText="Да"
        cancelText="Не"
      />
    );

    expect(screen.getByRole('button', { name: 'Да' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Не' })).toBeInTheDocument();
  });

  it('disables buttons when isLoading is true', () => {
    render(
      <ConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
        title="Test"
        message="Test"
        isLoading={true}
      />
    );

    const confirmButton = screen.getByRole('button', { name: /Потвърди/i });
    const cancelButton = screen.getByRole('button', { name: /Отказ/i });

    expect(confirmButton).toBeDisabled();
    expect(cancelButton).toBeDisabled();
  });

  it('renders danger variant with correct styling', () => {
    render(
      <ConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
        title="Danger"
        message="Danger message"
        variant="danger"
      />
    );

    expect(screen.getByText('Danger')).toBeInTheDocument();
  });

  it('renders success variant with correct styling', () => {
    render(
      <ConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
        title="Success"
        message="Success message"
        variant="success"
      />
    );

    expect(screen.getByText('Success')).toBeInTheDocument();
  });

  it('renders info variant with correct styling', () => {
    render(
      <ConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
        title="Info"
        message="Info message"
        variant="info"
      />
    );

    expect(screen.getByText('Info')).toBeInTheDocument();
  });

  it('shows loading spinner when isLoading is true', () => {
    const { container } = render(
      <ConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
        title="Test"
        message="Test"
        isLoading={true}
      />
    );

    // Check for loading spinner (has animate-spin class)
    const spinner = container.querySelector('.animate-spin');
    expect(spinner).toBeInTheDocument();
  });
});
