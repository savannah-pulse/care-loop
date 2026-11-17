// ESLint flat config for the messaging simulator ClientApp.
// Enforces the AGENTS.md documentation standard: every export (functions, components, hooks,
// interfaces and each of their properties, type aliases, enums, constants) needs a TSDoc comment,
// and comments must be valid TSDoc. Lint warnings fail the build (--max-warnings 0).
import prettier from 'eslint-config-prettier';
import jsdoc from 'eslint-plugin-jsdoc';
import reactHooks from 'eslint-plugin-react-hooks';
import tsdoc from 'eslint-plugin-tsdoc';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist/**', 'node_modules/**', 'coverage/**', '.yarn/**'] },
  ...tseslint.configs.strict,
  {
    files: ['**/*.{ts,tsx}'],
    plugins: { jsdoc, tsdoc, 'react-hooks': reactHooks },
    settings: { jsdoc: { mode: 'typescript' } },
    rules: {
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/exhaustive-deps': 'error',
      'tsdoc/syntax': 'error',
    },
  },
  {
    // Documentation is required on everything exported from application source.
    files: ['src/**/*.{ts,tsx}'],
    rules: {
      'jsdoc/require-jsdoc': [
        'error',
        {
          publicOnly: true,
          enableFixer: false,
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
      'jsdoc/require-description': ['error', { contexts: ['any'] }],
      'jsdoc/require-hyphen-before-param-description': ['error', 'always'],
      'jsdoc/check-param-names': 'error',
      'jsdoc/no-types': 'error',
    },
  },
  {
    // Plain functions and hooks must document parameters and return values.
    // Components (.tsx) document their props on the props type instead.
    files: ['src/**/*.ts'],
    rules: {
      'jsdoc/require-param': 'error',
      'jsdoc/require-param-description': 'error',
      'jsdoc/require-returns': 'error',
      'jsdoc/require-returns-description': 'error',
    },
  },
  prettier,
);
