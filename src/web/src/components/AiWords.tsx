import { useId } from 'react'
import { Icon } from './Icon'

/** Props for {@link AiChip}. */
export interface AiChipProps {
  /** The chip's word, for example `AI` or `AI draft`. */
  readonly label: string
}

/**
 * A small accent chip with a spark, marking words that AI wrote.
 *
 * @remarks
 * Decoration beside the caption that says in words that AI wrote the text, so it is hidden from
 * screen readers, which hear the caption instead.
 */
export function AiChip({ label }: AiChipProps) {
  return (
    <span className="ai-chip" aria-hidden="true">
      <Icon name="spark" />
      {label}
    </span>
  )
}

/** Props for {@link AiWords}. */
export interface AiWordsProps {
  /** What the agent wrote, exactly as the API returned it. Plain text: a blank line starts a new paragraph. */
  readonly text: string
  /** The caption saying that AI wrote it and what from, for example `Written by AI from your worklist.` */
  readonly caption: string
  /** A chip shown before the words, for example `AI`; none when absent. */
  readonly chip?: string
  /** An extra class for the figure, for its layout on one screen. */
  readonly className?: string
}

/**
 * Words an AI agent wrote, shown as plain paragraphs in a figure captioned with who wrote them.
 *
 * @remarks
 * The caption names the figure (`aria-labelledby`), so the words and the label that AI wrote them
 * are read as one. The text is shown as text, never as markup, and only ever arrives complete: when
 * an agent could not write it, the API answers 503 and the screen shows a calm note instead.
 */
export function AiWords({ text, caption, chip, className }: AiWordsProps) {
  const captionId = useId()
  const paragraphs = text
    .split(/\n\s*\n/)
    .map((paragraph) => paragraph.trim())
    .filter((paragraph) => paragraph !== '')
  const classes = ['ai-words', chip ? 'with-chip' : undefined, className].filter(Boolean).join(' ')
  return (
    <figure className={classes} aria-labelledby={captionId}>
      {chip ? <AiChip label={chip} /> : null}
      <div className="ai-text">
        {paragraphs.map((paragraph, index) => (
          // The paragraphs are a fixed text that never reorders, so their place is their identity.
          <p key={index}>{paragraph}</p>
        ))}
      </div>
      <figcaption className="ai-caption" id={captionId}>
        {caption}
      </figcaption>
    </figure>
  )
}
