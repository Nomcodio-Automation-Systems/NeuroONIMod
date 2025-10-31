---
applyTo: "**"
---
# Project general coding standards

## Naming Conventions
- Use PascalCase for component names, interfaces, and type aliases
- Use camelCase for variables, functions, and methods
- Prefix private class members with underscore (_)
- Use ALL_CAPS for constants

## Error Handling
- Use try/catch blocks for async operations
- Always log errors with contextual information

## Code Structure
- Organize code into modules based on functionality
- Keep components small and focused on a single responsibility
- Split classes into separate files when longer then 400 lines
- Don't use multi line commands in the cmd.


## Documentation
- Create docstrings for all public methods and classes
- Use param, return, pre and post  and description in the docstrings
- Write into the code comments that explain the purpose of the methods and classes
- Use Markdown format for README.md file
- Include usage examples in docstrings where applicable
- Use comments to explain complex logic, but avoid obvious comments
- Except for README.md, all documentation files should be in the `docs/` folder
- Don't create documentation files for trivial changes

## Testing
- Write unit tests for all components and utility functions
- Use descriptive test names that explain the purpose of the test
- Ensure tests cover both positive and negative cases
- Put tests depending on the context in their own subfolder
- Use mocking frameworks to isolate components during testing