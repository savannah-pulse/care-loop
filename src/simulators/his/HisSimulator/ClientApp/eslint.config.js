import { defineConfig } from 'eslint/config';
import prettier from 'eslint-config-prettier';
import jsdoc from 'eslint-plugin-jsdoc';
import reactHooks from 'eslint-plugin-react-hooks';
import tsdoc from 'eslint-plugin-tsdoc';
import tseslint from 'typescript-eslint';

/**
 * ESLint flat config for the HIS simulator ClientApp.
 *
 * Documentation is enforced as AGENTS.md requires: every export in `src/` (functions, components,
 * hooks, interfaces and each of their properties, type aliases, enums and constants) needs a
 * TSDoc comment, and the comment syntax itself is checked by `tsdoc/syntax`.
 */
export default defineConfig(
  { ignores: ['dist/**', 'node_modules/**', '.yarn/**'] },
  tseslint.configs.strict,
  {
    files: ['**/*.{ts,tsx}'],
    plugins: { 'react-hooks': reactHooks },
    rules: {
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/exhaustive-deps': 'error',
    },
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    plugins: { tsdoc, jsdoc },
    rules: {
      'tsdoc/syntax': 'error',
      'jsdoc/require-jsdoc': [
        'error',
        {
          publicOnly: true,
          checkConstructors: false,
          require: {
            ArrowFunctionExpression: true,
            ClassDeclaration: true,
            FunctionDeclaration: true,
            FunctionExpression: true,
            MethodDefinition: true,
          },
          contexts: [
            'TSInterfaceDeclaration',
            'TSTypeAliasDeclaration',
            'TSEnumDeclaration',
            'TSPropertySignature',
            'TSMethodSignature',
            'ExportNamedDeclaration > VariableDeclaration',
          ],
        },
      ],
    },
  },
  prettier,
);
